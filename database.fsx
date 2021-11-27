#load "bootstrap.fsx"
#r "nuget: Akka.FSharp" 
#r "nuget: Akka.TestKit" 

open System
open System.Collections.Generic
open System.Data
open System.IO
open Akka.Actor
open Akka.FSharp

let database:DataSet = new DataSet()

let userDataTable = new DataTable("Users")
userDataTable.Columns.Add("Username", typeof<string>)
userDataTable.Columns.Add("Password", typeof<string>)
userDataTable.Columns.Add("Firstname", typeof<string>)
userDataTable.Columns.Add("Lastname", typeof<string>)
userDataTable.Columns.Add("Email", typeof<string>)
userDataTable.Columns.Add("ActorObj", typeof<IActorRef>)
userDataTable.Columns.Add("Followers", typeof<list<string>>)
userDataTable.PrimaryKey <- [|userDataTable.Columns.["Username"]|]
database.Tables.Add(userDataTable)

let tweetDataTable = new DataTable("Tweets")
tweetDataTable.Columns.Add("TweetID", typeof<string>);
tweetDataTable.Columns.Add("Username", typeof<string>);
tweetDataTable.Columns.Add("Tweet", typeof<string>);
tweetDataTable.PrimaryKey <- [|tweetDataTable.Columns.["TweetID"]|]
database.Tables.Add(tweetDataTable)

let ReTweetDataTable = new DataTable("ReTweets")
ReTweetDataTable.Columns.Add("ReTweetID", typeof<string>);
ReTweetDataTable.Columns.Add("TweetID", typeof<string>);
ReTweetDataTable.Columns.Add("Username", typeof<string>);
ReTweetDataTable.Columns.Add("ReTweetUser", typeof<string>);
ReTweetDataTable.Columns.Add("Tweet", typeof<string>);
ReTweetDataTable.PrimaryKey <- [|ReTweetDataTable.Columns.["ReTweetID"]|]
database.Tables.Add(ReTweetDataTable)

let HashTagDataTable = new DataTable("HashTag")
HashTagDataTable.Columns.Add("HashTag", typeof<string>);
HashTagDataTable.Columns.Add("TweetList", typeof<List<string>>);
HashTagDataTable.PrimaryKey <- [|HashTagDataTable.Columns.["HashTag"]|]
database.Tables.Add(HashTagDataTable)

let MentionDataTable = new DataTable("Mention")
MentionDataTable.Columns.Add("Mention", typeof<string>);
MentionDataTable.Columns.Add("TweetList", typeof<List<string>>);
ReTweetDataTable.PrimaryKey <- [|MentionDataTable.Columns.["Mention"]|]
database.Tables.Add(ReTweetDataTable)

