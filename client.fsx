#load "bootstrap.fsx"
#load "database.fsx"
#load "datatype.fsx"

open System
open System.Threading
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Collections.Generic
open Datatype
open Akka.Configuration
open Akka.Serialization

let mutable keepActive = true
let ClientConfig = 
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
                    port = 8000
                    hostname = localhost
                    message-frame-size =  30000000b
                    send-buffer-size =  30000000b
                    receive-buffer-size =  30000000b
                    maximum-frame-size = 30000000b
                }
            }
        }")

let mutable numClients = 0
let mutable serverip = "localhost"
let mutable serverport = "9090"
let mutable maxRandomRequest =  100

if fsi.CommandLineArgs.Length > 1 then
    numClients <- fsi.CommandLineArgs.[1] |> int
    maxRandomRequest <- fsi.CommandLineArgs.[2] |> int
    serverip <- fsi.CommandLineArgs.[3] |> string
    serverport <- fsi.CommandLineArgs.[4] |> string

if numClients <= 0 || serverip = "" || serverport = "" then
    printfn "Invalid input"
    Environment.Exit(0)

let serverAddress = "akka.tcp://TwitterServer@" + serverip + ":" + serverport + "/user/TwitterServer"
let rand = Random(DateTime.Now.Millisecond)
let alphaNumeric = "abcdefghijklmnopqrstuvxyz1234567890"

let getRandomString (strlen) = 
    let mutable randomValue = ""
    for i in 0..strlen do
        let randomChar = alphaNumeric.Chars (rand.Next (alphaNumeric.Length - 1))
        randomValue <- randomValue + string(randomChar) 
    done
    randomValue

let randomTweetList = [
    "My game is glorious, and I want to drink beer. Everyone needs random coworkers, really. ";
    "My holiday season is a pain, and I want to slip away quietly. More fine criminals, I think. ";
    "My band is missing in action, and I want to change. A bit of solitary cowards, actually. ";
    "My worst enemy is so fragile, and I want to die trying. More faster friends, in the end. ";
    "My name is a nightmare, and I want to see the world. A bit of random friends, please. ";
    "My dad is fantastic, and I want to go to Mars. Slow dancing for free dates, for real. ";
    "My name is debt free, and I want to hear you say it. For all backwards heroes, for real.";
    "My cat is a joy, and I want to study algebra. A path towards casual beats, said no one ever.";
    "My normal twitter is hard labor, and I want to respect my elders. Excellent funky dates, man. ";
    "My groove is so fragile, and I want to go faster. Come for the perfect beats, you see. ";
    "My inheritance is a fairytale, and I want to wake up. Excellent backwards neighbors, bro.";
    "The Los Angeles Rams lose their third-straight game and fall to 7-4 after a 36-28 loss to the Green Bay Packers";
    "Omicron variant adds new peril to the holiday season in California and beyond. Here’s what you need to know";
    "‘We’re on the brink of collapse.’ What eight Ontario nurses have to say on the state of a profession in crisis";
    "'De-vaccination' is medically impossible. But some conspiracy theorists are encouraging people to try."
]

let randomHashTagList = [ "randomtweet"; "yolo2u"; "fishnut"; 
    "flashmacaroni"; "midnightbus"; "dinonut"; "luckymacaroni"; 
    "luckymacaroni"; "sillypop"; "flashbus"; "powerbite"; "dinkybag";
    "globalnut "; "maltedgold "; "bunny4ever"
]

let getRandomHashSubList(numOfTags) = 
    let mutable hashTagList = list.Empty
    for i in 0..numOfTags do
        let hashTag = randomHashTagList.[rand.Next(randomHashTagList.Length-1)]
        hashTagList <- "#" + hashTag :: hashTagList
    hashTagList

let getRandomMentionString(numOfTags) = 
    let mutable mentionStr = ""
    for i in 0..numOfTags do
        mentionStr <- mentionStr + " @User" + string (rand.Next(numClients-1))
    mentionStr

let getRandomTweet(numOfTags) = 
    let mutable numTagsToAppend = rand.Next(1,5)
    let mutable randomTweet = randomTweetList.[rand.Next(randomTweetList.Length-1)]
    randomTweet <- randomTweet + (getRandomHashSubList(numTagsToAppend) |> Core.String.concat " " )
    let probabilityNum = rand.Next(100)
    if (probabilityNum > 10) then
        randomTweet <- randomTweet + getRandomMentionString(numTagsToAppend)
    randomTweet + " " + string(rand.Next(100000))
//-------------------------------------- Client --------------------------------------//

let clientSystem = System.create "TwitterClient" ClientConfig

let viewTweets (username:string, tweets: list<tweetDetailsRecord>, printType: TweetTypeMessage, tweetType: TweetTypeMessage) =
    match printType with
        | Live ->
            for twt in tweets do printfn $"{username} received tweet twt.Tweet with tweet ID {twt.TweetID} and type {tweetType}"   
        | Search ->
            for twt in tweets do printfn $"{username} received tweet twt.Tweet from {twt.Username} and type {tweetType}"  
        | _ -> printfn "Unknown message type to print"

let ClientActor userId system (mailbox:Actor<_>) =
    let username = userId
    let password = getRandomString(10)
    let mutable isLoggedIn = false
    let mutable myTweets: list<tweetDetailsRecord> = list.Empty
    let mutable newTweetCount = 0
    
    let getFirstNTweets(maxCount: int, tweetList: list<tweetDetailsRecord>) =
        let mutable filterFrontList = list.Empty
        let number = if maxCount < tweetList.Length then maxCount else tweetList.Length
        for i in [0..number-1] do
            filterFrontList <- tweetList.[i] :: filterFrontList
        filterFrontList

    let ServerActObjRef = select (serverAddress) system

    let rec loop () = actor {
        let! message = mailbox.Receive()
        match message with
            | SignUpUser ->
                printfn $"User {username} requested to Sign up."
                let userDetails = new UserDetails(username, username + "@ufl.com", password, "akka.tcp://TwitterClient@localhost:8000/user/" + (string username))
                ServerActObjRef <! SignUpReqServer userDetails

            | LogOutUser -> 
                isLoggedIn <- false
                printfn $"User {username}  requested to Log out."
                let userObj = new UserLogOut(username, mailbox.Self)
                ServerActObjRef <! LogOutReqServer userObj

            | LogInUser -> 
                isLoggedIn <- true
                printfn $"User {username} requested to Log in."
                let userObj = new UserLogIn(username, password)
                ServerActObjRef <! LogInReqServer userObj
                let newTweets = getFirstNTweets(newTweetCount, myTweets)
                viewTweets(username, newTweets, Live, Live)
                newTweetCount <- 0
            
            | FollowUser(toFollowId) -> 
                if isLoggedIn then
                    printfn $"Recieved follow request for {toFollowId} from {username}" 
                    ServerActObjRef <! FollowReqServer (username, toFollowId)

            | SendTweetUser(tweet) -> 
                if isLoggedIn then
                    printfn $"User {username} requested to send tweet."
                    ServerActObjRef <! SendTweets(username, tweet + "- by" + username)

            | ReTweetUser -> 
                if myTweets.Length > 0 then
                    let randTweetID = rand.Next(myTweets.Length-1)
                    printfn $"User {username} requested to retweet."
                    let tweetObj = myTweets.[randTweetID]
                    ServerActObjRef <! ReTweets(username, tweetObj.TweetID)
                else
                    printfn $"User {username} don't have any tweet to share."
            
            | SearchTweetsWithHashTag searchKey -> 
                printfn $"User {username} requested to search hashtag {searchKey}."
                ServerActObjRef <! SearchHashtag(username, searchKey)

            | SearchTweetsWithMention -> 
                printfn $"User {username} requested to search his mentions."
                ServerActObjRef <! SearchMention(username, username)

            | ReceieveTweetUser(tweetList: list<tweetDetailsRecord>, userStatusType: TweetTypeMessage, tweetType: TweetTypeMessage) -> 
                printfn $"Recieved {tweetList.Length} new tweets for {username}" 
                match userStatusType with 
                    | Live ->
                        if isLoggedIn then
                            myTweets <- tweetList @ myTweets 
                            viewTweets(username, tweetList, Live, tweetType)
                    | Pending -> 
                        myTweets <- tweetList @ myTweets
                        newTweetCount <- newTweetCount + tweetList.Length
                    | Search ->
                        viewTweets(username, tweetList, Search, Search)
                    | _ -> printfn "Invalid message type for recieve"

            | UserRequestResponse (msg) ->
                printfn $"Got {msg} for {username}"

            | _ -> printfn  "Invalid operation"
        return! loop ()
    }
    loop ()

let mutable userMap = Map.empty
for id in 0..numClients do
    let username = ("User" + string id)
    userMap <- userMap.Add(username, (spawn clientSystem (username) (ClientActor username clientSystem)))

//-------------------------------------- Client --------------------------------------//


//-------------------------------------- Simulator --------------------------------------//

let mutable onlineUserList = new List<string>()

// Signing up users
for KeyValue(key, actorValue) in userMap do
    actorValue <! SignUpUser

System.Threading.Thread.Sleep(1000)

// Logining in user
for KeyValue(key, actorValue) in userMap do
    actorValue <! LogInUser
    onlineUserList.Add(key)

// Adding Random followers with zipf
for i in 0..numClients do
    let followee = userMap.["User"+string i]
    let rank =  (maxRandomRequest/(i+1)) |> int
    for j in 0..rank do
        let followerId = string (rand.Next(numClients-1))
        followee <! FollowUser("User" + followerId)

// Sharing random Tweets among users
for id in 0..numClients do
    let userObj = userMap.["User"+string id]
    let rank =  (maxRandomRequest/(id+1)) |> int
    for j in 0..rank do
        userObj <! SendTweetUser(getRandomTweet())

System.Threading.Thread.Sleep(2000)
// Sharing random ReTweets 
for id in 0..numClients do
    let username = "User" + string id
    if userMap.ContainsKey username then
        let userObj = userMap.[username]
        for j in 0..id do
            let probabilityNum = rand.Next(100)
            if (probabilityNum >= 50) then
                userObj <! ReTweetUser

// Searching for hashTags
for id in 0..numClients do
    let userObj = userMap.["User"+string id]
    for j in 0..id do
        let probabilityNum = rand.Next(100)
        if (probabilityNum >= 50) then
            let randomHashTag = "#" + randomHashTagList.[rand.Next(randomHashTagList.Length)]
            userObj <! SearchTweetsWithHashTag randomHashTag

// Searching for mentions
for id in 0..numClients do
    let userObj = userMap.["User"+string id]
    for j in 0..id do
        let probabilityNum = rand.Next(100)
        if (probabilityNum > 10) then
            userObj <! SearchTweetsWithMention

// Random simulator for all operations
let mutable numOperation = 0
while keepActive do
    let probabilityNum = rand.Next(100)
    numOperation <- numOperation + 1

    if numOperation = maxRandomRequest then 
        printfn "Exiting client"
        keepActive <- false
        let ServerActObjRef = select (serverAddress) clientSystem
        printfn "Done with making random requests"
        System.Threading.Thread.Sleep(3000)
        ServerActObjRef <! (PoisonPill.Instance)
        // Environment.Exit(0)

    if (probabilityNum < 25 && probabilityNum > 0) then 
        let randUserId = rand.Next(onlineUserList.Count)
        let randomUserLogout = userMap.[onlineUserList.[randUserId]]
        randomUserLogout <! LogOutUser
        onlineUserList.RemoveAt(randUserId) |> ignore
    else if (probabilityNum < 50 && probabilityNum > 25) then 
        let mutable randUserId = onlineUserList.[rand.Next(onlineUserList.Count)]
        if onlineUserList.Contains(randUserId) then randUserId <- onlineUserList.[rand.Next(onlineUserList.Count-1)]  
        let randomUserLogout = userMap.[randUserId]
        randomUserLogout <! LogInUser
        onlineUserList.Add(randUserId)
    else if (probabilityNum < 60 && probabilityNum > 50 ) then 
        let userObj = userMap.[onlineUserList.[rand.Next(onlineUserList.Count)]]
        userObj <! SendTweetUser(getRandomTweet())
    else if (probabilityNum < 70 && probabilityNum > 60 ) then 
        let userObj = userMap.[onlineUserList.[rand.Next(onlineUserList.Count)]]
        userObj <! ReTweetUser
    else if (probabilityNum < 80 && probabilityNum > 70 ) then 
        let userObj = userMap.[onlineUserList.[rand.Next(onlineUserList.Count)]]
        let randomHashTag = "#" + randomHashTagList.[rand.Next(randomHashTagList.Length)]
        userObj <! SearchTweetsWithHashTag randomHashTag
    else if (probabilityNum < 90 && probabilityNum > 80 ) then 
        let userObj = userMap.[onlineUserList.[rand.Next(onlineUserList.Count)]]
        userObj <! SearchTweetsWithMention
    else if (probabilityNum < 100 && probabilityNum > 90 ) then 
        let userObj = userMap.[onlineUserList.[rand.Next(onlineUserList.Count)]]
        let followerId = string (rand.Next(numClients-1))
        userObj <! FollowUser("User" + followerId)

Console.ReadLine() |> ignore
