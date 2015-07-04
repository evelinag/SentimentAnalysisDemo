// NOTE: The order of references matters here: http://fsprojects.github.io/FSharp.Data.Toolbox/TwitterProvider.html
#I @"./packages/FSharp.Data.Toolbox.Twitter/lib/net40"
#I @"./packages/FSharp.Data/lib/net40"
#r "FSharp.Data.Toolbox.Twitter.dll"
#r "FSharp.Data.dll"
#load @"packages/FsLab/FsLab.fsx"

open System
open System.IO

open FSharp.Data
open FSharp.Data.Toolbox.Twitter

open RProvider
open RProvider.``base``
open RProvider.graphics
open RProvider.ggplot2
open RDotNet
#load "ggplot.fs"
open FFplot

// ================================================
// IMPORTANT
// Register an app with Twitter at https://apps.twitter.com/app/new 
// to get the key and secret
let key = ""
let secret = ""
let twitter = Twitter.AuthenticateAppOnly(key, secret)
// ================================================

// 1. Community engagement
// ==================================================

let rec searchTweets hashtag lastTweetId remainingRequests tweets =
    printfn "%d" remainingRequests
    if remainingRequests = 0 then tweets else
    let ts = 
        match lastTweetId with
        | None -> 
            twitter.Search.Tweets(hashtag, count=100)
        | Some(id) -> 
            twitter.Search.Tweets(hashtag, maxId = id, count=100)
    if ts.Statuses.Length = 0 then tweets
    else 
        let lastId = ts.Statuses.[ts.Statuses.Length-1].Id |> Some
        searchTweets hashtag lastId (remainingRequests-1) 
            (Array.append tweets ts.Statuses)
        
// Compare engagement for different programming languages
let hashtag = "#fsharp"
let tweets = searchTweets hashtag None 100 [||]

for t in tweets do printfn "%A" t.Text

// Interactions
let interactions =
    tweets
    |> Array.filter (fun t -> 
        (t.Text.StartsWith("RT ")) || t.FavoriteCount > 0)

// Probability of interaction
let probInteraction = 
    (float interactions.Length)/(float tweets.Length)

printfn "Number of downloaded tweets: %d" 
    tweets.Length

// #fsharp : 83.8 % (1098 tweets in total)
// #csharp : 60.7 % (2766 tweets in total)
// #dotnet : 50,3 % (2861 tweets in total)

// 2. Growth of the community
// ==================================================
open Deedle
open XPlot.GoogleCharts

let [<Literal>] location = __SOURCE_DIRECTORY__ + @"/data/fsharp_2013-2014.csv"
type Tweets = CsvProvider<location>
let tweetHistory = Tweets.Load(location)

// What is in the table?
for row in tweetHistory.Rows do 
    printfn "%A" row.Text


// Get a list of people that tweeted each day
let tweetersByDate =
    tweetHistory.Rows
    |> Seq.map (fun tweet -> tweet.CreatedDate, tweet.FromUserScreenName)
    |> Seq.groupBy (fun (date, author) -> 
        System.DateTime(date.Year, date.Month, date.Day))
    |> Seq.sortBy fst
    |> Seq.map (fun (dt, ts) ->
        let tweeters = ts |> Seq.map (fun (d, author) -> author)
        dt, tweeters)
    |> Array.ofSeq

// Count number of unique people that tweeted each day
let countsByDay =
    tweetersByDate
    |> Array.map (fun (dt, tweeters) -> 
        let count = tweeters |> set |> Set.count |> float
        dt, count)

// Plot
Chart.Scatter(countsByDay)

// 3. When do people tweet?
// ================================================

let tweetsByTime =
    tweetHistory.Rows
    |> Seq.map (fun tweet -> tweet.CreatedDate)

// Day of week
tweetsByTime
|> Seq.countBy (fun t -> t.DayOfWeek)
|> Seq.sortBy fst
|> Seq.map (fun (d,n) -> d.ToString(), n)
|> Chart.Column

// By time of day
tweetsByTime
|> Seq.countBy (fun t -> (24 + (t.Hour - 2))%24 ) // utc
|> Seq.sortBy fst
|> Chart.Column









