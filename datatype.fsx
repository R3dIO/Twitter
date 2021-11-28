#load "bootstrap.fsx"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 

open System
open System.Collections.Generic
open Akka.Actor
open Akka.FSharp

type userDetails =
    struct
        val Username: string
        val Firstname: string
        val Lastname: string
        val Email: string
        val Password : string
        val Userobj : IActorRef
    end

type LogInUser =
    struct
        val username : string
        val password : string
    end

type LogOutUser =
    struct
        val username : string
        val actorObj : IActorRef
    end

type tweetDetails =
    struct
        val TweetID : string
        val Username : string
        val Tweet : string
    end

type userDetailsRecord = {
    Username: string;
    Firstname: string;
    Lastname: string;
    Email: string;
    Password: string;
    Userobj: IActorRef;
    Followers: list<string>;
}

type tweetDetailsRecord = {
    TweetID : string;
    Username : string;
    Tweet : string;
}

type ServerMessage = 
    | Login of LogInUser
    | Logout of LogOutUser
    | SignUpUser of userDetails
    | SendTweet of string * string
    | ReTweet of string * string

type ClientMessage = 
    | ReceieveTweet of tweetDetailsRecord

type TwitterMessage = 
    | Register of int64 * string * int
    | Logout of int64
    | Login of int64 * string
    | AddFollower of int64 * int64
    | Tweet of int64 * string
    | GetAllFollowing of int64
    | GetAllMentions of string * int64
    | GetAllHashTags of string * int64
    | LogoutUser 
    | LoginUser
    | TweetLive of string
    | PrintQueryResult of List<string>
    | Terminate
    | Subscribe of int64
    | SendTweet of string
    | QuerySubscribed of int
    | QueryTweetWithHashTag of string
    | QueryTweetWithMention of string
    | ReTweetSim of int
    | SendRetweet of string
    | CreateUsersDone of int64
    | SubscribeDone
    | SendTweetsDone
    | SendWithHashTagDone
    | SendWithMentioDone
    | QueryFirstNDone
    | QueryWithMentionDone of int64 * List<string>
    | QueryWithHashtagDone of int64 * List<string>
    | RetweetDone
    | StartSim
    | StartLogout
    | LogoutDone 
    | StartLogin
    | LoginDone
    | StartSubscribe
    | StartTweeting
    | StartQueryFirstN
    | QueryResult of List<string> * int64
    | StartQueryByMention
    | StartQueryByHashTag
    | StartReTweetSim
    | SendRetweetDone
