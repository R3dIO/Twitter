#load "bootstrap.fsx"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 

open System
open System.Collections.Generic
open Akka.Actor
open Akka.FSharp

type UserDetails =
    struct
        val Username: string
        val Email: string
        val Password : string
        val Userobj : string

        new (username, email, password, userobjPath) = 
            { Username = username; Email = email; Password = password; Userobj = userobjPath;}
    end

type UserLogIn =
    struct
        val Username : string
        val Password : string

        new (username, password) = {Username = username; Password = password;}
    end

type UserLogOut =
    struct
        val Username : string
        val ActorObj : IActorRef

        new (username, actorObj) = {Username = username; ActorObj = actorObj;}
    end

type tweetDetails =
    struct
        val TweetID : string
        val Username : string
        val Tweet : string
    end

type userDetailsRecord = {
    Username: string;
    Email: string;
    Password: string;
    Userobj: string;
    Followers: list<string>;
}

type tweetDetailsRecord = {
    TweetID : string;
    Username : string;
    Tweet : string;
}

type TweetTypeMessage = 
    | Live 
    | Pending
    | Search

type ServerMessage = 
    | SignUpReqServer of UserDetails
    | LogInReqServer of UserLogIn
    | LogOutReqServer of UserLogOut
    | FollowReqServer of string * string
    | SendTweets of string * string
    | ReTweets of string * string
    | SearchHashtag of string * string
    | SearchMention of string * string

type ClientMessage = 
    | LogInUser
    | LogOutUser
    | SignUpUser
    | SendTweetUser of string
    | ReTweetUser
    | FollowUser of string
    | SearchHashtagUser of string
    | SearchMentionUser of string
    | ReceieveTweetUser of list<tweetDetailsRecord> * TweetTypeMessage
    | SearchTweetsWithMention of string
    | SearchTweetsWithHashTag of string
