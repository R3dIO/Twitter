#r "nuget: Akka.FSharp"
#r "nuget: Akka.TestKit"
#r "nuget: Akka.Remote"
#r "nuget: Akka.Serialization.Hyperion"
#r "nuget: MathNet.Numerics"
#load "datatype.fsx"

open Akka.FSharp
open System
open System.Collections.Generic
open Datatype
open Akka.Configuration
open Akka.Serialization
open MathNet.Numerics.Distributions
open MathNet.Numerics.Random

let configuration =
    ConfigurationFactory.ParseString(
        @"akka {
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
                    port = 2552
                    hostname = localhost
                }
            }
        }"
    )

let clientSystem = System.create "Twitter" configuration

let serverRef =
    select ("akka.tcp://TwitterServer@192.168.0.94:9001/user/TwitterServer") clientSystem

let rand = Random(DateTime.Now.Millisecond)
let numUsers = fsi.CommandLineArgs.[1] |> int64
let numTweets = fsi.CommandLineArgs.[2] |> int

let Client userId system (mailbox: Actor<_>) =
    let userid = userId
    let mutable isLoggedIn = true
    let mutable myTweets = new List<string>()

    let takeNTweets firstn =
        let firstList = new List<string>()

        let number =
            if firstn < myTweets.Count then
                firstn
            else
                myTweets.Count

        for i in [ 0 .. number - 1 ] do
            firstList.Add(myTweets.[i])

        firstList

    let serverRef =
        select ("akka.tcp://TwitterServer@192.168.0.94:9001/user/TwitterServer") system

    let simRef =
        select ("akka://Twitter/user/Simulator") system

    let rec loop () =
        actor {
            let! message = mailbox.Receive()

            match message with
            | LoginUser ->
                isLoggedIn <- true
                //printfn "User %d logged in to Twitter successfully." userid
                simRef <! LoginDone

            | LogoutUser ->
                isLoggedIn <- false
                //printfn "User %d logged out from Twitter." userid
                simRef <! LogoutDone
            | Subscribe (toFollowId) -> //printfn "Recieved subscribe for %d from %d" toFollowId userId
                serverRef <! AddFollower(userid, toFollowId)
            //printfn "User %d followed User %d" userid toFollowId
            | SendTweet (tweet) -> //printfn "Send tweet recived"
                if isLoggedIn then
                    //printfn "User %d tweeted %s - by User %d" userid tweet userid
                    serverRef
                    <! Tweet(userid, tweet + "- by User " + (string userid))

                    myTweets.Add(tweet)
            | QuerySubscribed (firstN) ->
                printfn "query result got for %d" userId
                let mutable tweetList = takeNTweets firstN
                simRef <! QueryResult(tweetList, userId)
            | QueryTweetWithMention (key) -> serverRef <! GetAllMentions(key, userId)
            | QueryTweetWithHashTag (key) -> serverRef <! GetAllHashTags(key, userId)
            | PrintQueryResult (listOfTweets) ->
                for tweet in listOfTweets do
                    printfn "%s" tweet
            | TweetLive (tweet) -> if isLoggedIn then printfn "%s" tweet
            | ReTweetSim (firstN) ->
                let tweetList = takeNTweets firstN

                for tweet in tweetList do
                    mailbox.Self <! SendRetweet(tweet)
            | SendRetweet (tweet) ->
                serverRef
                <! Tweet(userid, tweet + "- by User " + (string userid))

                printfn "User %d retweeted %s - by User %d" userid tweet userid
                simRef <! SendRetweetDone
            | _ -> printfn "Wrong Message!!!!!"

            return! loop ()
        }

    loop ()

let simulator (mailbox: Actor<_>) =
    let mutable stopwatch = Diagnostics.Stopwatch.StartNew()
    let mutable createDone = 0L
    let mutable diffTime = 0L
    let mutable numToLogout = 0L
    let mutable listLoggedOut = List<int64>()
    let mutable numLoggedOut = 0L
    let mutable numLoggedIn = 0L
    let mutable numSubscribed = 0L
    let mutable numberSubscribe = 0L
    let mutable numTweetsSent = (int64 (numTweets + 2)) * numUsers
    let mutable userMentionList = List<int64>()
    let mutable numTweetsRcvd = 0L
    let mutable queryResultRcvd = 0L
    let mutable queryResultWithMenRcvd = 0L
    let mutable queryReceivedVar = 0L
    let mutable retweetCount = 0
    let mutable randomRetweetUser = 0L

    let randomListOfTweets =
        [ "Wishing you a very HAPPY BIRTHDAY, May you always shine like you do Dizzy symbol lots of love !"
          "So much credit to all of the brave men and women in state houses who are defending our great Constitution. Thank you!"
          "STANDUP! Take the training against street harassment"
          "Team India fast bowler Mohammed Siraj lost his father. Siraj has decided to stay with the Indian contingent."
          "My memoir, A Promised Land, is out today. I hope you’ll read it. My goal was to give you some insight into the events and people that shaped me during the early years of my presidency. Most of all, I hope it inspires you to see yourself playing a role in shaping a better world."
          "She’s sang, acted, and even had ME singing on national television. She’s written over three thousand songs that make you cry, challenge your cheating man, or inspire faith in you."
          "During the lockdown I had the time to do a lot of home gardening, where I learnt to pot my own plants. There truly is no better feeling than that!"
          "The FBI says it is \"aware\" of reports of robocalls telling voters to \"stay home and stay safe\" but is not commenting further"
          "Four days left to turn out voters in the most pivotal election of our lifetimes.
    "
          "Rudy Giuliani will be on Greg Kelly Reports. 7:15 P.M"
          "Lunar Excursion., It will have landing legs" ]

    let randomListOfTweetsWithHashTag =
        [ "#Actor Wishing you a very HAPPY BIRTHDAY, May you always shine like you do Dizzy symbol lots of love !"
          "#Trump So much credit to all of the brave men and women in state houses who are defending our great Constitution. Thank you!"
          "#BLM STANDUP! Take the training against street harassment"
          "#Cricket Team India fast bowler Mohammed Siraj lost his father. Siraj has decided to stay with the Indian contingent."
          "#USA My memoir, A Promised Land, is out today. I hope you’ll read it. My goal was to give you some insight into the events and people that shaped me during the early years of my presidency. Most of all, I hope it inspires you to see yourself playing a role in shaping a better world."
          "#USA She’s sang, acted, and even had ME singing on national television. She’s written over three thousand songs that make you cry, challenge your cheating man, or inspire faith in you."
          "#COVID During the lockdown I had the time to do a lot of home gardening, where I learnt to pot my own plants. There truly is no better feeling than that!"
          "#USA #News The FBI says it is \"aware\" of reports of robocalls telling voters to \"stay home and stay safe\" but is not commenting further"
          "#USA Four days left to turn out voters in the most pivotal election of our lifetimes.
    "
          "#Trump Rudy Giuliani will be on Greg Kelly Reports. 7:15 P.M"
          "#USA Lunar Excursion., It will have landing legs"
          "#COP5615 is great" ]

    let hashtags =
        [ "#Actor"
          "#Trump"
          "#USA"
          "#BLM"
          "#Cricket"
          "#COVID" ]

    let mutable userMap = Map.empty
    let mutable usersList = [ 0L .. numUsers - 1L ]
    let mutable subMap = Map.empty
    let gamma = Zipf(0.5, (int numUsers))

    let randomInt64 num =
        let byt = Array.create 8 (Byte())
        rand.NextBytes byt
        (Math.Abs(BitConverter.ToInt64(byt, 0)) % num)

    let zipFSample = gamma.Sample()

    numToLogout <- randomInt64 (numUsers / 2L)

    for id in usersList do
        let randomSub = (int64 zipFSample)
        subMap <- subMap.Add(id, randomSub)
        numSubscribed <- numSubscribed + randomSub

    printfn "NumLoggedout:%d" numToLogout
    printfn "Subscribed:%d" numSubscribed

    for id in [ 0L .. numUsers - 1L ] do
        userMap <- userMap.Add(id, (spawn clientSystem ("Client" + (string id)) (Client id clientSystem)))

    let createUsers nUsers nTweets =
        for id in [ 0L .. nUsers - 1L ] do
            serverRef
            <! Register(id, "passwd" + (string id), nTweets)

    let logoutUsers nToLogout =
        let listLoggedO = new List<int64>()

        for user in [ numUsers - nToLogout .. numUsers - 1L ] do
            if (not (listLoggedO.Contains(user))) then
                serverRef <! Logout(user)
                listLoggedO.Add(user)

        listLoggedO

    let loginUsers nToLogout =
        for user in [ numUsers - nToLogout .. numUsers - 1L ] do
            serverRef <! Login(user, "passwd" + (string user))

    let simulateSubscribe userId numToSubScribe =
        let listSubscribedAlready = new List<int64>()

        for i in [ 0L .. numToSubScribe - 1L ] do
            let id = randomInt64 numUsers
            userMap.[userId] <! Subscribe(id)
            listSubscribedAlready.Add(id)

    let simulateRandomTweetsFor userId =
        for i in [ 0 .. numTweets - 1 ] do
            let tweet =
                randomListOfTweets.Item(rand.Next(0, randomListOfTweets.Length - 1))
            // userRef <- select ("akka://Twitter/user/Client" + (string userId)) clientSystem
            userMap.[userId] <! SendTweet(tweet)

    let rec loop () =
        actor {

            let! message = mailbox.Receive()

            match message with
            | StartSim ->
                stopwatch.Restart()
                createUsers numUsers numTweets
            | CreateUsersDone (userId) ->
                createDone <- createDone + 1L

                if createDone = numUsers then
                    diffTime <- stopwatch.ElapsedMilliseconds
                    printfn "Created %d number of users in time: %d" numUsers diffTime
                    mailbox.Self <! StartLogout
            | StartLogout ->
                stopwatch.Restart()
                listLoggedOut <- logoutUsers numToLogout
            | LogoutDone ->
                numLoggedOut <- numLoggedOut + 1L
                //printfn "numlogo:%d" numLoggedOut
                if numLoggedOut = numToLogout then
                    diffTime <- stopwatch.ElapsedMilliseconds
                    printfn "Logged out %d random users in time: %d" numToLogout diffTime
                    mailbox.Self <! StartLogin
            | StartLogin ->
                stopwatch.Restart()
                loginUsers numToLogout
            | LoginDone ->
                numLoggedIn <- numLoggedIn + 1L
                //printfn "numlogi:%d" numLoggedIn
                if numLoggedIn = numToLogout then
                    diffTime <- stopwatch.ElapsedMilliseconds
                    printfn "Logged in %d users in time: %d" listLoggedOut.Count diffTime
                    mailbox.Self <! StartSubscribe
            | StartSubscribe ->
                printfn "Started subscribing using Zipf distribution:"
                stopwatch.Restart()

                for id in usersList do
                    //printfn "Subscribing %d for %d" subMap.[id] id
                    simulateSubscribe id subMap.[id]
            | SubscribeDone ->
                numberSubscribe <- numberSubscribe + 1L
                //printfn "NumSubs: %d to %d" numberSubscribe numSubscribed
                if numberSubscribe = numSubscribed then
                    diffTime <- stopwatch.ElapsedMilliseconds
                    printfn "Subscribed to users randomly in time: %d" diffTime
                    mailbox.Self <! StartTweeting
            | StartTweeting ->
                printfn "Started tweeting using Zipf distribution:"
                stopwatch.Restart()
                printfn "Started random tweeting"

                for id in usersList do
                    simulateRandomTweetsFor id

                printfn "Started hashtag tweeting"

                for id in usersList do
                    let tweet =
                        randomListOfTweetsWithHashTag.Item(rand.Next(0, randomListOfTweetsWithHashTag.Length - 1))
                    // userRef <- select ("akka://Twitter/user/Client" + (string id)) clientSystem
                    userMap.[id] <! SendTweet(tweet)

                printfn "Started random USER tweeting"

                for id in usersList do
                    let toUser = randomInt64 numUsers

                    let tweet =
                        randomListOfTweets.Item(rand.Next(0, randomListOfTweets.Length - 1))
                        + " @User"
                        + (string toUser)
                    // userRef <- select ("akka://Twitter/user/Client" + (string id)) clientSystem
                    userMap.[id] <! SendTweet(tweet)
                    userMentionList.Add(toUser)

                numTweetsSent <- (int64 (numTweets + 2)) * numUsers
            | SendTweetDone ->
                printfn "NumRecvdt %d torev %d" numTweetsRcvd numTweetsSent
                numTweetsRcvd <- numTweetsRcvd + 1L

                if numTweetsRcvd = numTweetsSent then
                    diffTime <- stopwatch.ElapsedMilliseconds

                    printfn
                        "Every user tweeted for %d times. Total number of times tweeted is: %d recvd:%d. Completed in time %d"
                        (numTweets + 2)
                        numTweetsSent
                        numTweetsRcvd
                        diffTime
                    mailbox.Self <! StartQueryFirstN
            | StartQueryFirstN ->
                stopwatch.Restart()
                printfn "Started random USER QUERYING %A" userMentionList

                for id in userMentionList do
                    // userRef <- select ("akka://Twitter/user/Client" + (string id)) clientSystem
                    userMap.[id] <! QuerySubscribed(5)
            | QueryResult (tweetList, userId) ->
                queryResultRcvd <- queryResultRcvd + 1L
                printfn "queryfirstNrecvd %d for %d" queryResultRcvd (int64 userMentionList.Count)
                printfn "User %d tweeted the following list of tweets %A" userId tweetList

                if queryResultRcvd = (int64 userMentionList.Count) then
                    diffTime <- stopwatch.ElapsedMilliseconds
                    printfn "%d Users queried for their first 5 tweets in time: %d" userMentionList.Count diffTime
                    mailbox.Self <! StartQueryByMention
            | StartQueryByMention ->
                stopwatch.Restart()
                printfn "Started random USER MENTION QUERYING %A" userMentionList

                for id in userMentionList do
                    let key = "User" + (string id)
                    userMap.[id] <! QueryTweetWithMention("@" + key)
                    queryReceivedVar <- int64 userMentionList.Count
            | QueryWithMentionDone (userId, tweetlist) ->
                queryResultWithMenRcvd <- queryResultWithMenRcvd + 1L
                printfn "queryResult with %d for %d" queryResultWithMenRcvd queryReceivedVar
                printfn "Got query results from user %d as %A" userId tweetlist

                if queryResultWithMenRcvd = queryReceivedVar then
                    diffTime <- stopwatch.ElapsedMilliseconds
                    printfn "%d Users queried for their mentions in time: %d" userMentionList.Count diffTime
                    mailbox.Self <! StartQueryByHashTag
                    queryResultWithMenRcvd <- 0L
            | StartQueryByHashTag ->
                stopwatch.Restart()
                printfn "Started random USER hashtag QUERYING"

                for tag in hashtags do
                    let randomUser = randomInt64 numUsers
                    // userRef <- select ("akka://Twitter/user/Client" + (string randomUser)) clientSystem
                    userMap.[randomUser] <! QueryTweetWithHashTag(tag)
                    queryReceivedVar <- int64 hashtags.Length
            | QueryWithHashtagDone (userId, tweetlist) ->
                queryResultWithMenRcvd <- queryResultWithMenRcvd + 1L
                printfn "queryResult with %d for %d" queryResultWithMenRcvd queryReceivedVar
                printfn "Got query results from user %d as %A" userId tweetlist

                if queryResultWithMenRcvd = queryReceivedVar then
                    diffTime <- stopwatch.ElapsedMilliseconds
                    printfn "%d Users queried for their mentions in time: %d" userMentionList.Count diffTime
                    mailbox.Self <! StartReTweetSim
            | StartReTweetSim ->
                printfn "Starting retweet"
                stopwatch.Restart()
                randomRetweetUser <- randomInt64 numUsers
                //let userRef = select ("akka://Twitter/user/Client" + (string randomRetweetUser)) clientSystem
                userMap.[randomRetweetUser] <! ReTweetSim(10)
            | SendRetweetDone ->
                retweetCount <- retweetCount + 1

                if retweetCount = 10 then
                    diffTime <- stopwatch.ElapsedMilliseconds
                    printfn "User %d retweet 10 tweets in time: %d" randomRetweetUser diffTime
                    printfn "Simulation Done"
            | _ -> printfn "Wrong Message!"

            return! loop ()
        }

    loop ()

let simulatorRef = spawn clientSystem "Simulator" simulator
printfn "%A" simulatorRef
simulatorRef <! StartSim
Console.ReadLine() |> ignore
