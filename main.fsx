#load "bootstrap.fsx"
#load "database.fsx"
#load "datatype.fsx"

open System
open Akka.Actor
open Akka.FSharp
open System.Collections.Generic
open System.Text.RegularExpressions
open FSharpPlus
open Suave
open System.Data
open Database
open Datatype

let numUsers = fsi.CommandLineArgs.[1] |> int

if numUsers <= 0 then
    printfn "Invalid input"
    Environment.Exit(0)

let mutable OnlineUsers :Map<string,IActorRef> = Map.empty
let mutable UserCount = 0;
let mutable followersMap: Map<string, list<string>> = Map.empty 
let mutable pendingTweets: Map<string, list<string>> = Map.empty 
//-------------------------------------- Initialization --------------------------------------//

//-------------------------------------- Server --------------------------------------//

let getHashNumFromSha1(s: string) = 
    System.Text.Encoding.ASCII.GetBytes(s) 
    |> System.Security.Cryptography.SHA1.Create().ComputeHash
    |> System.Text.Encoding.ASCII.GetString

let UpdateHashTagAndMentions (tweet: string) =
        let hashtagsList = new List<string>()
        let mentionsList = new List<string>()
        
        let hashtagsMatchCollection = Regex.Matches(tweet, "#[a-zA-Z0-9_]+")
        for hashtag in hashtagsMatchCollection do
            hashtagsList.Add(hashtag.Value)
            let exp = "hashtag = '"+hashtag.Value+"'"
            let tweeRows = HashTagDataTable.Select(exp)
            if tweeRows.Length = 0 then
                let row = HashTagDataTable.NewRow()
                row.["hashtag"] <- hashtag
                htable.Rows.Add row
            let dataRows = htable.Select(exp)
            let h = Hashtag(int(string(dataRows.[0].["id"])),string(dataRows.[0].["hashtag"]))
            tweetHashtags <- h :: tweetHashtags
            let row = httable.NewRow()
            row.["hashtagid"] <- h.ID
            row.["tweetid"] <- tweetrow.["id"]

        let mentionsMatchCollection = Regex.Matches(tweet, "@User[0-9]+")
        for mentionsMatch in mentionsMatchCollection do
            mentionsList.Add(mentionsMatch.Value)



let GetUserDetails(username: string) =
    let userExpression = "Username = '" + username + "'"
    let userDetailRows = (userDataTable.Select(userExpression))
    let mutable UserDetails = {Username=""; Firstname=""; Lastname=""; Email=""; Password=""; Userobj=null; Followers=[]}
    if (userDetailRows.Length > 0) then
        let userDetailRow = userDetailRows.[0]        
        let UserName = userDetailRow.Field(userDataTable.Columns.Item(0))
        let UserPasswd = userDetailRow.Field(userDataTable.Columns.Item(1))
        let FirstName = userDetailRow.Field(userDataTable.Columns.Item(2))
        let LastName = userDetailRow.Field(userDataTable.Columns.Item(3))
        let Mail = userDetailRow.Field(userDataTable.Columns.Item(4))
        let UserObj = userDetailRow.Field(userDataTable.Columns.Item(5))
        let FollowersList = userDetailRow.Field(userDataTable.Columns.Item(6))
        UserDetails <- { Username=UserName; Firstname=FirstName; Lastname=LastName; Email=Mail; Password=UserPasswd; Userobj=UserObj; Followers=FollowersList }
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
    let userdata = GetUserDetails(username)
    let followerLocalList = followersMap.TryFind(username)
    let mutable followerList = list.Empty
   
    match followerLocalList with
        | Some(followerLocalList) -> 
            printfn "Found %i followers for user %s" followerLocalList.Length username
            followerList <- followerLocalList
        | None ->
            if userdata.Followers.IsEmpty then
                printfn "Current user has no followers to share tweet"
            else 
                followerList <- userdata.Followers
    followerList
            
let RegisterUser (userInfo:userDetails) =
    let tempRow = userDataTable.NewRow()
    tempRow.SetField("Username", userInfo.Username)
    tempRow.SetField("FirstName", userInfo.Firstname)
    tempRow.SetField("LastName", userInfo.Lastname)
    tempRow.SetField("Email", userInfo.Email)
    tempRow.SetField("Password",userInfo.Password)
    tempRow.SetField("ActorObj",userInfo.Userobj)
    tempRow.SetField("Followers",[])
    userDataTable.Rows.Add(tempRow)
    UserCount <- UserCount + 1

let LoginUser (userCreds: LogInUser) =
    let mutable loginExpression = "Username = '" + userCreds.username + "'"
    let mutable userDetailRows = (userDataTable.Select(loginExpression))
    if (userDetailRows.Length > 0) then
        let userDetailRow = userDetailRows.[0]
        let UserName  = userDetailRow.Field(userDataTable.Columns.Item(0))
        let UserPasswd = userDetailRow.Field(userDataTable.Columns.Item(1))
        let UserObj = userDetailRow.Field(userDataTable.Columns.Item(5))
        if (UserPasswd = userCreds.password) then
            OnlineUsers <- OnlineUsers.Add(UserName, UserObj)
            if (pendingTweets.ContainsKey(userCreds.username)) then
                let localTweetList = pendingTweets.[userCreds.username]
                for tweet in localTweetList do
                    let tweetObj = GetTweetDetails(tweet)
                    UserObj <! ReceieveTweet tweetObj
                pendingTweets <- pendingTweets.Add(userCreds.username, [])

let LogOutUser (userCreds: LogOutUser) =
    if (OnlineUsers.ContainsKey(userCreds.username)) then
        OnlineUsers <- OnlineUsers.Remove(userCreds.username)

let FollowUser (followee: string, follower: string) =
    let userdata = GetUserDetails(followee)
    let followers = [follower] @ userdata.Followers
    let followerLocalList = followersMap.TryFind(followee)
    match followerLocalList with
        | Some(followerLocalList) -> followersMap <- followersMap.Add(followee, followerLocalList)
        | None -> followersMap <- followersMap.Add(followee, followers)

let SendTweets (username: string, tweet: string) =
    let tempRow = tweetDataTable.NewRow()
    let tweetHash = string (getHashNumFromSha1(username + tweet))
    tempRow.SetField("TweetID", tweetHash)
    tempRow.SetField("Username", username)
    tempRow.SetField("Tweet", tweet)
    tweetDataTable.Rows.Add(tempRow)

    let userTweet = { TweetID=tweetHash; Username=username; Tweet=tweet}
    let followerList = GetFollowers(username)
    for users in followerList do
        if (OnlineUsers.ContainsKey(users)) then
            OnlineUsers.[users] <! ReceieveTweet userTweet
        else
            if (pendingTweets.ContainsKey(users)) then
                pendingTweets <- pendingTweets.Add(users, [users] @ [userTweet.TweetID])
            else    
                pendingTweets <- pendingTweets.Add(users, [userTweet.TweetID])

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
            OnlineUsers.[users] <! ReceieveTweet userTweet
        else
            if (pendingTweets.ContainsKey(users)) then
                pendingTweets <- pendingTweets.Add(users, [users] @ [userTweet.TweetID])
            else    
                pendingTweets <- pendingTweets.Add(users, [userTweet.TweetID])

let Server(mailbox: Actor<_>) =
    
    let mutable searchCount = 0

    let rec loop()= actor{
        let! msg = mailbox.Receive();
        let response = mailbox.Sender();
        try
            match msg with 
                | SignUpUser (userData) -> 
                   RegisterUser userData

                | Login (userCreds:LogInUser) ->
                   LoginUser userCreds

                | Logout (userCreds:LogOutUser) ->
                   LogOutUser userCreds

                | SendTweets (username: string, tweet: string) ->
                    SendTweets (username, tweet)

                | ReTweets (username: string, tweetID: string) ->
                    ReTweets (username, tweetID)

                | _ -> ()
        with
            | :? System.IndexOutOfRangeException -> printfn "ERROR: Tried to access outside array!" |> ignore
        return! loop()
    }            
    loop()