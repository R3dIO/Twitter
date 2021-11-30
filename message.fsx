#r "nuget: Akka.FSharp" 

open Akka.Actor

type RegisterMsg =
    | RegisterUser of string*IActorRef
    | Login of string*IActorRef
    | Logout of string*IActorRef

type TweetHandlerMsg =
    | AddTweet of string*string*string*IActorRef
    | ReTweet of string*IActorRef

type GetHandlerMsg =
    | GetTweets of string*IActorRef
    | GetTags of string*IActorRef
    | GetHashTags of string * string * IActorRef

type TweetParserMsg = 
    | Parse of string*string*string*IActorRef

type FollowersMsg = 
    | Add of string*string*IActorRef
    | Update of string*string*IActorRef

type SimulatorMsg =
    | Start
    | StartAck
    | SubsAck

