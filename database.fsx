// #load "bootstrap.fsx"
open System.Data

let database:DataSet = new DataSet()

let userDataTable = new DataTable("Users")
userDataTable.Columns.Add("Username", typeof<string>)
userDataTable.Columns.Add("Password", typeof<string>)
userDataTable.Columns.Add("Firstname", typeof<string>)
userDataTable.Columns.Add("Lastname", typeof<string>)
userDataTable.Columns.Add("Email", typeof<string>)
userDataTable.Columns.Add("ActorObjPath", typeof<string>)
userDataTable.Columns.Add("Followers", typeof<string>)
userDataTable.PrimaryKey <- [|userDataTable.Columns.["Username"]|]
database.Tables.Add(userDataTable)

let tweetDataTable = new DataTable("Tweets")
let TweetIDCol = tweetDataTable.Columns.Add("TweetID", typeof<string>);
tweetDataTable.Columns.Add("Username", typeof<string>);
tweetDataTable.Columns.Add("Tweet", typeof<string>);
tweetDataTable.PrimaryKey <- [|TweetIDCol|]
database.Tables.Add(tweetDataTable)

let ReTweetDataTable = new DataTable("ReTweets")
let ReTweetIDCol = ReTweetDataTable.Columns.Add("ReTweetID", typeof<string>);
ReTweetDataTable.Columns.Add("TweetID", typeof<string>);
ReTweetDataTable.Columns.Add("Username", typeof<string>);
ReTweetDataTable.Columns.Add("ReTweetUser", typeof<string>);
ReTweetDataTable.Columns.Add("Tweet", typeof<string>);
// ReTweetDataTable.PrimaryKey <- [|ReTweetIDCol|]
database.Tables.Add(ReTweetDataTable)

let HashTagDataTable = new DataTable("HashTag")
let HashTagId = HashTagDataTable.Columns.Add("HashTagID", typeof<int32>);
// HashTagId.AutoIncrement = true;
// HashTagId.AutoIncrementSeed = 1L;
// HashTagId.AutoIncrementStep = 1L;
HashTagDataTable.Columns.Add("HashTag", typeof<string>);
HashTagDataTable.Columns.Add("TweetID", typeof<string>);
// HashTagDataTable.PrimaryKey <- [|HashTagDataTable.Columns.["HashTagID"]|]
database.Tables.Add(HashTagDataTable)

let MentionDataTable = new DataTable("Mention")
let MentionID = MentionDataTable.Columns.Add("MentionID", typeof<int32>);
// MentionID.AutoIncrement = true;
// MentionID.AutoIncrementSeed = 1L;
// MentionID.AutoIncrementStep = 1L;
MentionDataTable.Columns.Add("Mention", typeof<string>);
MentionDataTable.Columns.Add("TweetID", typeof<string>);
// MentionDataTable.PrimaryKey <- [|MentionDataTable.Columns.["MentionID"]|]
database.Tables.Add(MentionDataTable)

