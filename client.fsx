#load "bootstrap.fsx"
#load "database.fsx"
#load "datatype.fsx"

open System
open System.Threading
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Collections.Generic
open Database
open Datatype
open Akka.Configuration
open Akka.Serialization

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
                    port = 0
                    hostname = localhost
                }
            }
        }")

let numClients = fsi.CommandLineArgs.[1] |> int

if numClients <= 0 then
    printfn "Invalid input"
    Environment.Exit(0)

let rand = Random(DateTime.Now.Millisecond)

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
    let mutable password = ""
    let mutable isLoggedIn = false
    let mutable myTweets: list<tweetDetailsRecord> = list.Empty
    let mutable newTweetCount = 0

    let ServerActObjRef = select ("akka.tcp://ServerActor@192.168.0.94:9001/user/TwitterServer") system
    let rec loop () = actor {
        let! message = mailbox.Receive()
        match message with
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
                newTweetCount <- 0
                viewTweets(username,  myTweets, Live)
            
            | FollowUser(toFollowId) -> 
                if isLoggedIn then
                    printfn "Recieved follow request for %s from %s" toFollowId username
                    ServerActObjRef <! FollowReqServer (username, toFollowId)

            | SendTweetUser(tweet) -> 
                printfn "Send tweet recived"
                if isLoggedIn then
                    printfn "User %s tweeted %s" username tweet
                    ServerActObjRef <! SendTweets(username, tweet+"- by User "+ username)

            | ReceieveTweetUser(tweetList: list<tweetDetailsRecord>, tweetType: TweetTypeMessage) -> 
                printfn "Recieved %i new tweets for user %s" tweetList.Length username
                match tweetType with 
                    | Live ->
                        if isLoggedIn then
                            myTweets <- myTweets @ tweetList
                            viewTweets(username, myTweets, Live)
                    | Pending -> 
                        myTweets <- myTweets @ tweetList
                        newTweetCount <- newTweetCount + tweetList.Length
                    | Search ->
                        viewTweets(username, tweetList, Search)
                    
            
            | QueryTweetWithMention(key) -> serverRef <! GetAllMentions(key, userId)
            | QueryTweetWithHashTag(key) -> serverRef <! GetAllHashTags(key, userId)
            | PrintQueryResult(listOfTweets) -> for tweet in listOfTweets do
                                                    printfn "%s" tweet
            | TweetLive(tweet) -> if isLoggedIn then
                                    printfn "%s" tweet
            | ReTweetSim(firstN) -> let tweetList = takeNTweets firstN
                                    for tweet in tweetList do 
                                        mailbox.Self <! SendRetweet(tweet)
            | SendRetweet(tweet) -> serverRef <! Tweet(userid, tweet+"- by User "+(string userid))
                                    printfn "User %d retweeted %s - by User %d" userid tweet userid
                                    ServerActObjRef <! SendRetweetDone
            | _ -> printfn  "Invalid operation"
        return! loop ()
    }
    loop ()

let clients = Array.zeroCreate (numClients + 1)
for id in 0..numClients do
    clients.[id] <- spawn clientSystem ("User"+(string id)) ClientActor