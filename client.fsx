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
                helios.tcp {
                    port = 8000
                    hostname = localhost
                }
            }
        }")

let numClients = fsi.CommandLineArgs.[1] |> int

if numClients <= 0 then
    printfn "Invalid input"
    Environment.Exit(0)

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
        let hashTag = randomHashTagList.[rand.Next(randomHashTagList.Length)]
        hashTagList <- "#" + hashTag :: hashTagList
    hashTagList
    
//-------------------------------------- Client --------------------------------------//

let clientSystem = System.create "TwitterClient" ClientConfig

let viewTweets (username:string, tweets: list<tweetDetailsRecord>, printType: TweetTypeMessage) =
    match printType with
        | Live ->
            for twt in tweets do printfn "User %s received tweet %s with tweet ID %s" username twt.Tweet twt.TweetID
        | Search ->
            for twt in tweets do printfn "User %s received tweet %s from user %s" username twt.Tweet twt.Username 
        | _ -> printfn "Unknown message type from print"

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

    let ServerActObjRef = select ("akka.tcp://ServerActor@localhost:9090/user/TwitterServer") system

    let rec loop () = actor {
        let! message = mailbox.Receive()
        match message with
            | SignUpUser ->
                let userDetails = new UserDetails(username, username + "@ufl.com", password, "akka.tcp://ClientActor@localhost:8000/user/Client" + (string username))
                ServerActObjRef <! SignUpReqServer userDetails

            | LogOutUser -> 
                isLoggedIn <- false
                printfn "User %s logged out from Twitter." username
                let userObj = new UserLogOut(username, mailbox.Self)
                ServerActObjRef <! LogOutReqServer userObj

            | LogInUser -> 
                isLoggedIn <- true
                printfn "User %s logged in to Twitter successfully." username
                let userObj = new UserLogIn(username, password)
                ServerActObjRef <! LogInReqServer userObj
                let newTweets = getFirstNTweets(newTweetCount, myTweets)
                viewTweets(username, newTweets, Live)
                newTweetCount <- 0
            
            | FollowUser(toFollowId) -> 
                if isLoggedIn then
                    printfn "Recieved follow request for %s from %s" toFollowId username
                    ServerActObjRef <! FollowReqServer (username, toFollowId)

            | SendTweetUser(tweet) -> 
                printfn "Send tweet recieved"
                if isLoggedIn then
                    printfn "User %s tweeted %s" username tweet
                    ServerActObjRef <! SendTweets(username, tweet+"- by User "+ username)

            | ReTweetUser -> 
                let tweetObj = myTweets.[rand.Next(myTweets.Length)]
                ServerActObjRef <! ReTweets(username, tweetObj.TweetID)
            
            | SearchTweetsWithMention(searchKey) -> 
                ServerActObjRef <! SearchHashtag(username, searchKey)

            | SearchTweetsWithHashTag(searchKey) -> 
                ServerActObjRef <! SearchHashtag(username, searchKey)

            | ReceieveTweetUser(tweetList: list<tweetDetailsRecord>, tweetType: TweetTypeMessage) -> 
                printfn "Recieved %i new tweets for user %s" tweetList.Length username
                match tweetType with 
                    | Live ->
                        if isLoggedIn then
                            myTweets <- tweetList @ myTweets 
                            viewTweets(username, myTweets, Live)
                    | Pending -> 
                        myTweets <- tweetList @ myTweets
                        newTweetCount <- newTweetCount + tweetList.Length
                    | Search ->
                        viewTweets(username, tweetList, Search)

            | UserRequestResponse (msg) ->
                printfn "Got %s for user %s" msg username

            | _ -> printfn  "Invalid operation"
        return! loop ()
    }
    loop ()

let mutable userMap = Map.empty
for id in 0..numClients do
    let username = ("User" + string id)
    userMap <- userMap.Add(username, (spawn clientSystem ("Client"+(string id)) (ClientActor username clientSystem)))

//-------------------------------------- Client --------------------------------------//


//-------------------------------------- Simulator --------------------------------------//

let mutable onlineUserList = new List<string>()

// Signing up users
for KeyValue(key, actorValue) in userMap do
    printfn "Signing up user key %s" key
    actorValue <! SignUpUser

// Logining in user
for KeyValue(key, actorValue) in userMap do
    printfn "Loging In user key %s" key
    actorValue <! LogInUser
    onlineUserList.Add(key)

// Adding Random followers with zipf
for i in 0..numClients do
    let followee = userMap.["User"+string i]
    for j in 0..i do
        let followerId = string (rand.Next(numClients))
        followee <! FollowUser("User" + followerId)

// Sharing random Tweets among users
for id in 0..numClients do
    let followee = userMap.["User"+string id]
    for j in 0..id do
        let mutable probabilityNum = rand.Next(5)
        let mutable randomTweet = randomTweetList.[rand.Next(randomTweetList.Length)]
        randomTweet <- randomTweet + (getRandomHashSubList(probabilityNum) |> List.fold (+) "")
        probabilityNum <- rand.Next(100)
        if (probabilityNum > 70) then
            randomTweet <- randomTweet + "@User" + string (rand.Next(numClients)) + "@User" + string (rand.Next(numClients))
        followee <! SendTweetUser randomTweet

// Sharing random ReTweets 
for id in 0..numClients do
    let followee = userMap.["User"+string id]
    for j in 0..id do
        let probabilityNum = rand.Next(100)
        if (probabilityNum > 50) then
            followee <! ReTweetUser

// Searching for hashTags
for id in 0..numClients do
    let followee = userMap.["User"+string id]
    for j in 0..id do
        let probabilityNum = rand.Next(100)
        if (probabilityNum > 50) then
            let randomHashTag = randomHashTagList.[rand.Next(randomHashTagList.Length)]
            followee <! SearchTweetsWithMention (randomHashTag)

// Searching for mentions
for id in 0..numClients do
    let followee = userMap.["User"+string id]
    for j in 0..id do
        let probabilityNum = rand.Next(100)
        if (probabilityNum > 50) then
            let randomMention = "@User" + string (rand.Next(userMap.Count))
            followee <! SearchTweetsWithHashTag randomMention

// Random logouts and login
    // while keepActive do
    //     let probabilityNum = rand.Next(100)
    //     if (probabilityNum > 50) then
    //         let randomUserLogout = userMap.[  ]
    //         randomUserLogout <! LogOutUser