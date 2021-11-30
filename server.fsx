#load "bootstrap.fsx"
#load "database.fsx"
#load "datatype.fsx"

open System
open System.Collections.Generic
open System.Text.RegularExpressions
open System.Data
open Akka.Actor
open Akka.FSharp
open Akka.Configuration
open Akka.Serialization
open Database
open Datatype

let useDataTable = true;
let printUpdate = false;
let mutable UserCount = 0;
let mutable TweetCount = 0;
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
                helios.tcp {
                    port = 9090
                    hostname = localhost
                }
            }
        }")

let serverSystem = System.create "TwitterServer" serverConfig

//-------------------------------------- Initialization --------------------------------------//

//-------------------------------------- Server --------------------------------------//

let getHashNumFromSha1(s: string) = 
    System.Text.Encoding.ASCII.GetBytes(s) 
    |> System.Security.Cryptography.SHA1.Create().ComputeHash
    |> System.Text.Encoding.ASCII.GetString

let GetUserDetails(username: string) =
    let userExpression = "Username = '" + username + "'"
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
    let tweetExpression = "TweetID = '" + tweetId + "'"
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
            printfn "Found %i followers for user %s" followerLocalList.Count username
            followerList <- Set.toList followerLocalList
        | None ->
            if userdata.Followers = "" then
                printfn "Current user has no followers to share tweet"
            else 
                followerList <- splitLine userdata.Followers
    followerList

let SearchHashTagAndMentions (searchString: string, searchType: string) =
        let mutable tweetList = list.Empty
        if (searchType = "HashTag") then
            if (useDataTable) then
                let HashTagExpression = "HashTag = '" + searchString + "'"
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
                let MentionExpression = "Mention = '" + searchString + "'"
                let MentionDetailRows = (HashTagDataTable.Select(MentionExpression))
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
            insertTempRow.["HashTag"] <- hashtag
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
                let insertTempRow = HashTagDataTable.NewRow()
                insertTempRow.["Mention"] <- mention
                insertTempRow.["TweetID"] <- tweetID
                HashTagDataTable.Rows.Add(insertTempRow)
            else
                if (mentionsMap.ContainsKey(mention.Value)) then
                    mentionsMap <- mentionsMap.Add(mention.Value, mentionsMap.[mention.Value] @ [tweetID])
                else
                    mentionsMap <- mentionsMap.Add(mention.Value, [tweetID])
        else 
            printfn "User Does not exist" 
  
let Register (userInfo: UserDetails) =
    let tempRow = userDataTable.NewRow()
    tempRow.SetField("Username", userInfo.Username)
    tempRow.SetField("Email", userInfo.Email)
    tempRow.SetField("Password",userInfo.Password)
    tempRow.SetField("ActorObjPath",userInfo.Userobj)
    tempRow.SetField("Followers","")
    userDataTable.Rows.Add(tempRow)
    UserCount <- UserCount + 1

let LogIn (userCreds: UserLogIn) =
    let mutable response = ""
    let mutable loginExpression = "Username = '" + userCreds.Username + "'"
    let mutable userDetailRows = (userDataTable.Select(loginExpression))
    if (userDetailRows.Length > 0) then
        let userDetailRow = userDetailRows.[0]
        let UserName  = userDetailRow.Field(userDataTable.Columns.Item(0))
        let UserPasswd = userDetailRow.Field(userDataTable.Columns.Item(1))
        let UserObjPath = userDetailRow.Field(userDataTable.Columns.Item(5))
        let UserObj = serverSystem.ActorSelection(UserObjPath.ToString())
        if (UserPasswd = userCreds.Password) then
            OnlineUsers <- OnlineUsers.Add(UserName, UserObj)
            response <- response + ":" + "User logged in succesfully" + UserName
            if (pendingTweets.ContainsKey(userCreds.Username)) then
                let localTweetIdList = pendingTweets.[userCreds.Username]
                let localTweetList = localTweetIdList |> List.map(fun tweetID -> GetTweetDetails(tweetID))
                UserObj <! ReceieveTweetUser(localTweetList,Pending)
                pendingTweets <- pendingTweets.Add(userCreds.Username, [])
        else
            response <- response + ":" + "Incorrect password"
    else 
        response <- response + ":" + "User not found"
    response

let LogOut (userCreds: UserLogOut) =
    let mutable response = ""
    if (OnlineUsers.ContainsKey(userCreds.Username)) then
        OnlineUsers <- OnlineUsers.Remove(userCreds.Username)
    else
        response <- response + ":" + "User Isn't logged in"
    response

let Follow (followee: string, follower: string) =
    let mutable response = ""
    let userdata = GetUserDetails(followee)
    let userdataFollower = GetUserDetails(follower)
    if (userdata.Username <> "" && userdataFollower.Username <> "") then
        if useDataTable then
            let row:DataRow = userDataTable.NewRow()
            row.["Username"] <- userdata.Username
            row.["Followers"] <- userdata.Followers + ";" + userdataFollower.Username
            userDataTable.Rows.Add row
        else
            let followerLocalList = followersMap.TryFind(followee)
            match followerLocalList with
                | Some(followerLocalList) -> followersMap <- followersMap.Add(followee, followerLocalList)
                | None -> followersMap <- followersMap.Add(userdata.Username, (followersMap.[userdata.Username]).Add(follower))
    else
        response <- response + ":" + "Followe or Follower does not exist"
    response


let SendTweets (username: string, tweet: string) =
    let tempRow = tweetDataTable.NewRow()
    let tweetHash = string (getHashNumFromSha1(username + tweet))
    tempRow.SetField("TweetID", tweetHash)
    tempRow.SetField("Username", username)
    tempRow.SetField("Tweet", tweet)
    tweetDataTable.Rows.Add(tempRow)

    let userTweet = { TweetID=tweetHash; Username=username; Tweet=tweet}
    UpdateHashTagAndMentions(tweet, userTweet.TweetID)
    let followerList = GetFollowers(username)
    for users in followerList do
        if (OnlineUsers.ContainsKey(users)) then
            OnlineUsers.[users] <! ReceieveTweetUser([userTweet], Live)
        else
            if (pendingTweets.ContainsKey(users)) then
                pendingTweets <- pendingTweets.Add(users, [users] @ [userTweet.TweetID])
            else    
                pendingTweets <- pendingTweets.Add(users, [userTweet.TweetID])
    TweetCount <- TweetCount + 1

let ReTweets (username: string, tweetID: string) =
    let userTweet = GetTweetDetails(tweetID)
    let followerList = GetFollowers(username)

    for users in followerList do
        let tweetHash = string (getHashNumFromSha1(username + userTweet.Tweet + users))
        let tempRow = tweetDataTable.NewRow()
        tempRow.SetField("ReTweetID", tweetHash)
        tempRow.SetField("TweetID", userTweet.TweetID)
        tempRow.SetField("Username", username)
        tempRow.SetField("ReTweetUser", users)
        tempRow.SetField("Tweet", userTweet.Tweet)
        userDataTable.Rows.Add(tempRow)

        if (OnlineUsers.ContainsKey(users)) then
            OnlineUsers.[users] <! ReceieveTweetUser([userTweet],Live)
        else
            if (pendingTweets.ContainsKey(users)) then
                pendingTweets <- pendingTweets.Add(users, [users] @ [userTweet.TweetID])
            else    
                pendingTweets <- pendingTweets.Add(users, [userTweet.TweetID])

let ServerActor(mailbox: Actor<_>) =
    
    let mutable searchCount = 0

    let rec loop()= actor{
        let! msg = mailbox.Receive();
        let response = mailbox.Sender();
        try
            match msg with 
                | SignUpReqServer (userData: UserDetails) -> 
                    if printUpdate then printfn "User %s reqested to register" userData.Username
                    Register userData

                | LogInReqServer (userCreds: UserLogIn) ->
                    if printUpdate then printfn "User %s reqested to login" userCreds.Username
                    let response = LogIn userCreds
                    let actorObj = select (GetUserDetails(userCreds.Username).Userobj) serverSystem
                    actorObj <! UserRequestResponse response

                | LogOutReqServer (userCreds: UserLogOut) ->
                    if printUpdate then printfn "User %s reqested to logout" userCreds.Username
                    let response = LogOut userCreds
                    let actorObj = select (GetUserDetails(userCreds.Username).Userobj) serverSystem
                    actorObj <! UserRequestResponse response

                | FollowReqServer (followeID: string, followerID: string) ->
                    if printUpdate then printfn "User %s reqested to follow %s" followeID followerID
                    let response = Follow (followeID, followerID)
                    let actorObj = select (GetUserDetails(followeID).Userobj) serverSystem
                    actorObj <! UserRequestResponse response

                | SendTweets (username: string, tweet: string) ->
                    SendTweets (username, tweet)

                | ReTweets (username: string, tweetID: string) ->
                    ReTweets (username, tweetID)

                | SearchHashtag (username: string, searchString: string) ->
                    let userTweetList = SearchHashTagAndMentions (searchString, "HashTag")
                    if (OnlineUsers.ContainsKey(username)) then
                        OnlineUsers.[username] <! ReceieveTweetUser(userTweetList, Live)

                | SearchMention (username: string, searchString: string) ->
                    let userTweetList = SearchHashTagAndMentions (searchString, "Mention")
                    if (OnlineUsers.ContainsKey(username)) then
                        OnlineUsers.[username] <! ReceieveTweetUser(userTweetList, Live)

                | _ -> printfn "Invalid operation"
        with
            | :? System.IndexOutOfRangeException -> printfn "ERROR: Tried to access outside array!" |> ignore
        return! loop()
    }            
    loop()

let server = spawn serverSystem "TwitterServer" (ServerActor)
printfn "server: %A" server.Path
Console.ReadLine() |> ignore
//-------------------------------------- Server --------------------------------------//
