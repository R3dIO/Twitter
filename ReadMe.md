# COP5615 - Distributed Operating System Principles
## Project 4 Part 1 
## Twitter Clone and a client tester/simulator

Author: 
Anuj Koli - UFID: 97977572 
Pratiksha Jain - UFID: 96115195

## Introduction
In this project we have implemented a twitter clone. We have implemented a twitter engine and a simulator that will test the twitter engine functionalities. The engine and simulator are two independent processes and can be connected remotely.  

## What is working
### Twitter engine/Server

- Register account - Registers a user with properties: Username, Email, Password, Actor path and Followers. The Actor path is object of client which is used for communication between Client and Server. After web socket implementation, this parameter will be removed. 
- Login user - Verifies the user information to log it in to the system. The logged in users, i.e. actors, are stored in a list of online users which are used to perform send tweet, retweet and searches. 
- Logout user - Logs out a user by removing it from the list of online users.
- Send tweet - When a tweet is received, we store it into the database created and then we send the tweet to the users that subscribed for that user. In case, a user is not online then the tweets are added to pending tweets list. which will be resolved after subscriber user logs in.
- Retweet - In case of retweet, everything is similar to send tweet except that we store this retweet, user who originally tweeted and user who retweeted in a new table. 
- Follow user - A user can follow other user and be subscribed to the tweets and retweets of that user. A list of followers is maintained for each user and everytime a user does a tweet or retweet, those followers are notified of the action.
- Search Hashtag and Mentions - The hashtags and mentions are searched into the database where tweets are stored. We return the tweet containing the requested hashtag or mention.


### Simulator
The simulator is responsible for creating clients and registering those clients on the server. Those clients will then perform actions on the server following the Zipf distribution. The actions are performed in random manner with fixed probabilities for each action.

### Remote Implementation
The server and simulator are implemented such that they can run on independent machines using the server ip and server port.

### Zipf Distribution 
Each client is given a rank from 1 - N where N is the total number of clients that are simulated by the simulator. The number requests made by each client on the server will be 1/rank number per millisecond. The below graph shows the distribution of client as per its rank. We can analyse from it that each client is inversely proportional to the assigned rank.

## How to run

To run the program, you need to be inside the directory where code exists.

To start the server aka twitter engine -
> dotnet fsi $--$langversion:preview server.fsx


To start the client aka simulator -
> dotnet fsi $--$langversion:preview simulator.fsx $<$number of clients$>$ $<$number of requests$>$ $<$server ip$>$ $<$server port$>$

## Statistics

| Num Users | Total Tweets | Total Searches | Total Requests | Total Retweets |
|-----------|--------------|----------------|----------------|----------------|
| 10  	    | 6088	       | 65	            | 6309	         |  27            |
| 50	    | 9098	       | 139	        | 9935	         |  594           |
| 100	    | 10508	       | 1031	        | 14257	         |  2508          |
| 200	    | 11982	       | 2745	        | 25179	         |  10046         |
| 500	    | 14144	       | 14344	        | 89793	         |  60293         |

## What is the largest network you managed to deal with

