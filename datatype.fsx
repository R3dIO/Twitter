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