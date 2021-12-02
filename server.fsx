#time "on"
#load "bootstrap.fsx"
#load "database.fsx"
#load "datatype.fsx"

open System
open System.Text
open System.Text.RegularExpressions
open System.Data
open System.Security.Cryptography
open Akka.Actor
open Akka.FSharp
open Akka.Configuration
open Database
open Datatype

let useDataTable = true;
let printUpdate = false;
let mutable UserCount = 0;
let mutable TweetCount = 0;
let mutable ReTweetCount = 0;
let mutable SearchCount = 0;
let mutable RequestCount = 0;
let mutable FollowerCount = 0;
let mutable LogOutCount = 0;
let mutable LogInCount = 0;
let mutable OnlineUsers :Map<string,ActorSelection> = Map.empty
let mutable followersMap: Map<string, Set<string>> = Map.empty 
let mutable pendingTweets: Map<string, list<string>> = Map.empty 
let mutable hashtagsMap: Map<string, list<string>> = Map.empty
let mutable mentionsMap: Map<string, list<string>> = Map.empty

let serverConfig = 
    ConfigurationFactory.ParseString(
        @"akka {
            log-config-on-start : on
            stdout-loglevel : DEBUG
            loglevel : ERROR
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                debug : {
                    receive : on
                    autoreceive : on
                    lifecycle : on
                    event-stream : on
                    unhandled : on
                }
                serializers {
                    hyperion = ""Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion""
                }
                serialization-bindings {
                    ""System.Object"" = hyperion
                }                
            }
            remote {
                maximum-payload-bytes = 30000000 bytes
                helios.tcp {
                    port = 9090
                    hostname = localhost
                    message-frame-size =  30000000b
                    send-buffer-size =  30000000b
                    receive-buffer-size =  30000000b
                    maximum-frame-size = 30000000b
                }
            }
        }")

let serverSystem = System.create "TwitterServer" serverConfig
let rand = Random(DateTime.Now.Millisecond)

let getHashNumFromSha1(str: string) = 
    str + string(rand.Next(100000))
    |> Encoding.ASCII.GetBytes
    |> (new SHA256Managed()).ComputeHash
    |> System.BitConverter.ToString

let GetUserDetails(username: string) =
    let userExpression = $"Username = '{username}'"
    let userDetailRows = (userDataTable.Select(userExpression))
    let mutable UserDetails = {Username=""; Email=""; Password=""; Userobj=null; Followers=""}
    if (userDetailRows.Length > 0) then
        let userDetailRow = userDetailRows.[0]        
        let UserName = userDetailRow.Field(userDataTable.Columns.Item(0))
        let UserPasswd = userDetailRow.Field(userDataTable.Columns.Item(1))
        let Mail = userDetailRow.Field(userDataTable.Columns.Item(4))
        let UserObj = userDetailRow.Field(userDataTable.Columns.Item(5))
        let FollowersList = userDetailRow.Field(userDataTable.Columns.Item(6))
        UserDetails <- { Username=UserName; Email=Mail; Password=UserPasswd; Userobj=UserObj; Followers=FollowersList }
    UserDetails

let GetTweetDetails(tweetId: string) =
    let tweetExpression = $"TweetID = '{tweetId}'"
    let tweetDetailRows = (tweetDataTable.Select(tweetExpression))
    let mutable TweetDetails = {TweetID="";  Username=""; Tweet=""}
    if (tweetDetailRows.Length > 0) then
        let tweetDetailRow = tweetDetailRows.[0]        
        let tweetID = tweetDetailRow.Field(tweetDataTable.Columns.Item(0))
        let username = tweetDetailRow.Field(tweetDataTable.Columns.Item(1))
        let tweet = tweetDetailRow.Field(tweetDataTable.Columns.Item(2))
        TweetDetails <- { TweetID=tweetID;  Username=username; Tweet=tweet }
    TweetDetails

let GetFollowers(username: string) =
    let splitLine = (fun (line : string) -> Seq.toList (line.Split ';'))
    let userdata = GetUserDetails(username)
    let followerLocalList = followersMap.TryFind(username)
    let mutable followerList = list.Empty
   
    match followerLocalList with
        | Some(followerLocalList) -> 
            if printUpdate then printfn $"Found {followerLocalList.Count} followers for {username}"
            followerList <- Set.toList followerLocalList
        | None ->
            if userdata.Followers = "" then
                if printUpdate then printfn "Current user has no followers to share tweet"
            else 
                followerList <- splitLine userdata.Followers
    followerList

let SearchHashTagAndMentions (searchString: string, searchType: string) =
        let mutable tweetList = list.Empty
        if (searchType = "HashTag") then
            if (useDataTable) then
                let HashTagExpression = $"HashTag = '{searchString}'"
                let HashTagDetailRows = (HashTagDataTable.Select(HashTagExpression))
                if (HashTagDetailRows.Length > 0) then
                    for tweetID in HashTagDetailRows do
                        let tweetObj = GetTweetDetails(tweetID.Field("TweetID"))
                        tweetList <- tweetObj :: tweetList
            else
                if (hashtagsMap.ContainsKey(searchString)) then
                    for tweetID in hashtagsMap.[searchString] do
                        let tweetObj = GetTweetDetails(tweetID)
                        tweetList <- tweetObj :: tweetList

        elif (searchType = "Mention") then
            if (useDataTable) then
                let MentionExpression = $"Mention = '{searchString}'"
                let MentionDetailRows = (MentionDataTable.Select(MentionExpression))
                if (MentionDetailRows.Length > 0) then
                    for tweetID in MentionDetailRows do
                        let tweetObj = GetTweetDetails(tweetID.Field("TweetID"))
                        tweetList <- tweetObj :: tweetList
            else
                if (mentionsMap.ContainsKey(searchString)) then
                    for tweetID in mentionsMap.[searchString] do
                        let tweetObj = GetTweetDetails(tweetID)
                        tweetList <- tweetObj :: tweetList
        else 
            printfn "Invalid Search Type"
        tweetList

let UpdateHashTagAndMentions (tweet: string, tweetID: string) =
    let hashtagsMatchCollection = Regex.Matches(tweet, "#[a-zA-Z0-9_]+")
    for hashtag in hashtagsMatchCollection do
        if useDataTable then
            let insertTempRow = HashTagDataTable.NewRow()
            insertTempRow.["HashTag"] <- hashtag.Value
            insertTempRow.["TweetID"] <- tweetID
            HashTagDataTable.Rows.Add(insertTempRow)
        else
            if (hashtagsMap.ContainsKey(hashtag.Value)) then
                hashtagsMap <- hashtagsMap.Add(hashtag.Value, hashtagsMap.[hashtag.Value] @ [tweetID])
            else
                hashtagsMap <- hashtagsMap.Add(hashtag.Value, [tweetID])

    let mentionsMatchCollection = Regex.Matches(tweet, "@User[0-9]+")
    for mention in mentionsMatchCollection do
        let username = mention.Value.[1..]
        let userDetails = GetUserDetails(username)
        if (userDetails.Username <> "") then
            if useDataTable then
                let insertTempRow = MentionDataTable.NewRow()
                insertTempRow.["Mention"] <- username
                insertTempRow.["TweetID"] <- tweetID
                MentionDataTable.Rows.Add(insertTempRow)
            else
                if (mentionsMap.ContainsKey(mention.Value)) then
                    mentionsMap <- mentionsMap.Add(mention.Value, mentionsMap.[mention.Value] @ [tweetID])
                else
                    mentionsMap <- mentionsMap.Add(mention.Value, [tweetID])
        else 
            printfn "User Does not exist" 
  
let Register (userInfo: UserDetails) =
    let mutable response = "Register"
    let checkUserExist = GetUserDetails(userInfo.Username)
    if checkUserExist.Username = "" then
        let tempRow = userDataTable.NewRow()
        tempRow.SetField("Username", userInfo.Username)
        tempRow.SetField("Email", userInfo.Email)
        tempRow.SetField("Password",userInfo.Password)
        tempRow.SetField("ActorObjPath",userInfo.Userobj)
        tempRow.SetField("Followers","")
        userDataTable.Rows.Add(tempRow)
        UserCount <- UserCount + 1
        response <- response + " : " + "User Successfully registered"
    else 
        response <- response + " : " + "User already exists"
    response

let LogIn (userCreds: UserLogIn) =
    let mutable response = "LogIn"
    let mutable loginExpression = $"Username = '{userCreds.Username}'"
    let mutable userDetailRows = (userDataTable.Select(loginExpression))
    if (userDetailRows.Length > 0) then
        let userDetailRow = userDetailRows.[0]
        let UserName  = userDetailRow.Field(userDataTable.Columns.Item(0))
        let UserPasswd = userDetailRow.Field(userDataTable.Columns.Item(1))
        let UserObjPath = userDetailRow.Field(userDataTable.Columns.Item(5))
        let UserObj = serverSystem.ActorSelection(UserObjPath.ToString())
        if (UserPasswd = userCreds.Password) then
            OnlineUsers <- OnlineUsers.Add(UserName, UserObj)
            response <- response + ": " + "User logged in succesfully" + UserName
            if (pendingTweets.ContainsKey(userCreds.Username)) then
                let localTweetIdList = pendingTweets.[userCreds.Username]
                let localTweetList = localTweetIdList |> List.map(fun tweetID -> GetTweetDetails(tweetID))
                UserObj <! ReceieveTweetUser(localTweetList,Pending, Tweet)
                pendingTweets <- pendingTweets.Add(userCreds.Username, [])
        else
            response <- response + " : " + "Incorrect password"
    else 
        response <- response + " : " + "User not found"
    response

let LogOut (userCreds: UserLogOut) =
    let mutable response = "LogOut"
    if (OnlineUsers.ContainsKey(userCreds.Username)) then
        OnlineUsers <- OnlineUsers.Remove(userCreds.Username)
    else
        response <- response + " : " + "User Isn't logged in"
    response

let Follow (followee: string, follower: string) =
    let mutable response = "Follow"
    let userdata = GetUserDetails(followee)
    let userdataFollower = GetUserDetails(follower)
    if (userdata.Username <> "" && userdataFollower.Username <> "") then
        if useDataTable then
            let row = userDataTable.Select("Username='" + followee + "'")
            row.[0].["Username"] <- userdata.Username
            row.[0].["Followers"] <- userdata.Followers + ";" + userdataFollower.Username
            response <- response + " : " + $"{userdata.Username} is now followed by {userdataFollower.Username}" 
        else
            let followerLocalList = followersMap.TryFind(followee)
            match followerLocalList with
                | Some(followerLocalList) -> followersMap <- followersMap.Add(followee, followerLocalList)
                | None -> followersMap <- followersMap.Add(userdata.Username, (followersMap.[userdata.Username]).Add(follower))
    else
        response <- response + " : " + "Followe or Follower does not exist"
    response


let SendTweets (username: string, tweet: string) =
    let mutable response = "SendTweet"
    let tweetHash = string (getHashNumFromSha1(username + tweet + DateTime.Now.ToLongTimeString() + string (rand.Next(UserCount)) ))
    let getTweetDetails = GetTweetDetails(tweetHash)
    if getTweetDetails.TweetID = "" && tweetHash <> "" then
        let tempRow = tweetDataTable.NewRow()
        TweetCount <- TweetCount + 1
        tempRow.SetField("TweetID", tweetHash)
        tempRow.SetField("Username", username)
        tempRow.SetField("Tweet", tweet)
        tweetDataTable.Rows.Add(tempRow)

        let userTweet = { TweetID=tweetHash; Username=username; Tweet=tweet}
        UpdateHashTagAndMentions(tweet, userTweet.TweetID)
        let followerList = GetFollowers(username)
        for users in followerList do
            if (OnlineUsers.ContainsKey(users)) then
                if printUpdate then printfn $"Send tweet {userTweet} to user {users}"
                OnlineUsers.[users] <! ReceieveTweetUser([userTweet], Live, Tweet)
            else
                if (pendingTweets.ContainsKey(users)) then
                    pendingTweets <- pendingTweets.Add(users, [users] @ [userTweet.TweetID])
                else    
                    pendingTweets <- pendingTweets.Add(users, [userTweet.TweetID])
        response <- response + " : " + "Successfully shared tweet with TweetID" + userTweet.TweetID
    else
        response <- response + " : " + "User already shared tweet"
    response

let ReTweets (username: string, tweetID: string) =
    let mutable response = "ReTweet"
    let userTweet = GetTweetDetails(tweetID)
    let followerList = GetFollowers(username)
    ReTweetCount <- ReTweetCount + 1

    for users in followerList do
        let tweetHash = string (getHashNumFromSha1(username + userTweet.Tweet + users + DateTime.Now.ToLongTimeString()))
        let tempRow = ReTweetDataTable.NewRow()
        tempRow.SetField("ReTweetID", tweetHash)
        tempRow.SetField("TweetID", userTweet.TweetID)
        tempRow.SetField("Username", username)
        tempRow.SetField("ReTweetUser", users)
        tempRow.SetField("Tweet", userTweet.Tweet)
        ReTweetDataTable.Rows.Add(tempRow)

        if (OnlineUsers.ContainsKey(users)) then
            OnlineUsers.[users] <! ReceieveTweetUser([userTweet],Live, ReTweet)
        else
            if (pendingTweets.ContainsKey(users)) then
                pendingTweets <- pendingTweets.Add(users, [users] @ [userTweet.TweetID])
            else    
                pendingTweets <- pendingTweets.Add(users, [userTweet.TweetID])
    response <- response + " : " + "Successfully Retweeted tweet with TweetID" + userTweet.TweetID
    response

let ServerActor(mailbox: Actor<_>) =
    let rec loop()= actor{
        let! msg = mailbox.Receive();
        RequestCount <- RequestCount + 1
        try
            match msg with 
                | SignUpReqServer (userData: UserDetails) -> 
                    if printUpdate then printfn $"User {userData.Username} reqested to register" 
                    let response = Register userData
                    let actorObj = select (GetUserDetails(userData.Username).Userobj) serverSystem
                    actorObj <! UserRequestResponse response

                | LogInReqServer (userCreds: UserLogIn) ->
                    LogInCount <- LogInCount + 1
                    if printUpdate then printfn $"User {userCreds.Username} reqested to login"
                    let response = LogIn userCreds
                    let actorObj = select (GetUserDetails(userCreds.Username).Userobj) serverSystem
                    actorObj <! UserRequestResponse response

                | LogOutReqServer (userCreds: UserLogOut) ->
                    LogOutCount <- LogOutCount + 1
                    if printUpdate then printfn $"User {userCreds.Username} reqested to logout"
                    let response = LogOut userCreds
                    let actorObj = select (GetUserDetails(userCreds.Username).Userobj) serverSystem
                    actorObj <! UserRequestResponse response

                | FollowReqServer (followeID: string, followerID: string) ->
                    FollowerCount <- FollowerCount + 1
                    if printUpdate then printfn $"User {followeID} reqested to follow {followerID}"
                    let response = Follow (followeID, followerID)
                    let actorObj = select (GetUserDetails(followeID).Userobj) serverSystem
                    actorObj <! UserRequestResponse response

                | SendTweets (username: string, tweet: string) ->
                    TweetCount <- TweetCount + 1
                    if printUpdate then printfn $"{username} tweeted {tweet}"
                    let response = SendTweets (username, tweet)
                    let actorObj = select (GetUserDetails(username).Userobj) serverSystem
                    actorObj <! UserRequestResponse response

                | ReTweets (username: string, tweetID: string) ->
                    if printUpdate then printfn $"{username} retweeted {tweetID}"
                    let response = ReTweets (username, tweetID)
                    let actorObj = select (GetUserDetails(username).Userobj) serverSystem
                    actorObj <! UserRequestResponse response

                | SearchHashtag (username: string, searchString: string) ->
                    SearchCount <- SearchCount + 1
                    let userTweetList = SearchHashTagAndMentions (searchString, "HashTag")
                    if (OnlineUsers.ContainsKey(username)) then
                        OnlineUsers.[username] <! ReceieveTweetUser(userTweetList, Live, Search)
                        let response = $"{username} Search Found hashtag {searchString} in {userTweetList.Length} tweets "
                        let actorObj = select (GetUserDetails(username).Userobj) serverSystem
                        actorObj <! UserRequestResponse response

                | SearchMention (username: string, searchString: string) ->
                    SearchCount <- SearchCount + 1
                    let userTweetList = SearchHashTagAndMentions (searchString, "Mention")
                    if (OnlineUsers.ContainsKey(username)) then
                        let response = $"{username} Search Found UID {searchString} in {userTweetList.Length} tweets "
                        OnlineUsers.[username] <! ReceieveTweetUser(userTweetList, Live, Search)
                        let actorObj = select (GetUserDetails(username).Userobj) serverSystem
                        actorObj <! UserRequestResponse response

                | _ -> printfn "Invalid operation"
        with
            | :? System.IndexOutOfRangeException -> printfn "ERROR: Tried to access outside array!" |> ignore
        return! loop()
    }            
    loop()

let server = spawn serverSystem "TwitterServer" (ServerActor)
printfn "server: %A" server.Path
Console.ReadLine() |> ignore
printfn $"NumUsers = {UserCount}, 
        Total Tweets = {TweetCount}, 
        Searches = {SearchCount}, 
        Request Count={RequestCount}, 
        Retweets = {ReTweetCount} 
        Follower Count = {FollowerCount} 
        LogOut Count = {LogOutCount}
        LogIn Count = {LogInCount}"
