// NOTE: The order of references matters here: http://fsprojects.github.io/FSharp.Data.Toolbox/TwitterProvider.html
#I @"./packages/FSharp.Data.Toolbox.Twitter/lib/net40"
#I @"./packages/FSharp.Data/lib/net40"
#r "FSharp.Data.Toolbox.Twitter.dll"
#r "FSharp.Data.dll"
open FSharp.Data
open FSharp.Data.Toolbox.Twitter

#r "packages/Suave/lib/net40/Suave.dll"

// ================================================
// IMPORTANT
// Register an app with Twitter at https://apps.twitter.com/app/new 
// to get the key and secret
let key = "CoqmPIJ553Tuwe2eQgfKA"
let secret = "dhaad3d7DreAFBPawEIbzesS1F232FnDsuWWwRTUg"
let twitter = Twitter.AuthenticateAppOnly(key, secret)
// ================================================

open System
open System.IO
open Suave
open Suave.Web
open Suave.Types
open Suave.Http
open Suave.Http.Successful
open Suave.Http.RequestErrors
open Suave.Http.Applicatives

#load "sentiment.fsx"

let tweet2html tweet sentiment =
   "<div class=\"row\">
         <div class=\"col-sm-1 form-inline\" id=\"sentiment\">
            <img src=\"web/" + string sentiment + ".png\" height=\"64\" width=\"64\" vspace=20 />
         </div>
         <div class=\"col-sm-8\" id=\"tweets\">
            " + tweet + "
         </div>
      </div> "

// ------------------------------------------------------------------
let hashtag = "#ProgNET15"

type Tweet = {
  User: string
  Text : string
  TweetID : int64 
  EmbeddedCode: string
  Sentiment : int
  }

type TweetMessage =
  | Post of Tweet
  | Retrieve of int64 * AsyncReplyChannel<string>
  | GetLastID of AsyncReplyChannel<int64 option>

let twitterAgent =
  MailboxProcessor.Start(fun inbox ->
    let rec loop messages = async {
      let! msg = inbox.Receive()
      match msg with
      | Post(tweet) ->
          return! loop (tweet::messages)

      | Retrieve(lastId, repl) ->
          let newMessages = messages |> List.filter (fun m -> m.TweetID > lastId)
          let newLastId = 
            if newMessages.Length > 0 then newMessages.Head.TweetID |> string
            else lastId |> string

          let html = 
            newMessages
            |> List.map (fun m -> tweet2html m.EmbeddedCode m.Sentiment ) 
            |> String.concat "<p>\n" 
          repl.Reply(newLastId + "\n" + html)
          
          return! loop messages 

      | GetLastID repl -> 
          if messages.Length > 0 then
              repl.Reply(Some messages.Head.TweetID)
          else 
              repl.Reply(None)
          return! loop messages }
    loop [] )


type EmbeddedTweet = JsonProvider<"https://api.twitter.com/1/statuses/oembed.json?url=https://twitter.com/Interior/status/463440424141459456">
let getEmbeddedCode tweetID = 
    let e = EmbeddedTweet.Load("https://api.twitter.com/1/statuses/oembed.json?url=https://twitter.com/Interior/status/" + string tweetID)
    e.Html

// ------------------------------------------------------------------

/// Check Twitter for new messages
let rec scanTweets () = async {
  printfn "Checking for messages..."
  let! lastID = twitterAgent.PostAndAsyncReply(fun ch -> GetLastID ch)
  let standardSleep = 6000
  let longSleep = 15 * 60 * 1000

  let newTweets, sleepTime = 
      try 
          match lastID with
          | Some(id) -> 
             twitter.Search.Tweets(hashtag, sinceId=id, count=50).Statuses, standardSleep
          | None -> // do intial search for tweets
             twitter.Search.Tweets(hashtag, count=100).Statuses, standardSleep
      with
      | :? System.Net.WebException as e -> // Twitter server refused the request
            // Sleep for one time window :-(
            printfn "%A" e
            printfn "Sleeping for 15 minutes"
            [||], longSleep

  printfn "number of new tweets: %A" newTweets.Length
  if newTweets.Length > 0 then
    // Add new messages from oldest to newest
    newTweets
    |> Array.filter (fun tweet -> not (tweet.Text.StartsWith("RT ")))
    |> Array.rev
    |> Array.iter (fun tweet ->
        let s = Sentiment.evaluateSentiment tweet.Text
        let message = 
            { User = tweet.User.Name; 
              TweetID = tweet.Id; Text = tweet.Text
              EmbeddedCode = getEmbeddedCode tweet.Id 
              Sentiment = s} 
        twitterAgent.Post(Post(message))) 

  do! Async.Sleep(sleepTime)
  return! scanTweets () }

/// Get new messages for the website
let getMessages lastId ctx = async {
  let! html = twitterAgent.PostAndAsyncReply(fun ch -> Retrieve(lastId, ch)) 
  return! OK html ctx }
   
let index = File.ReadAllText(__SOURCE_DIRECTORY__ + "/web/chat.html")

// ------------------------------------------------------------------

let cts = new System.Threading.CancellationTokenSource()
Async.Start(scanTweets(), cts.Token)
let stop () = cts.Cancel()

let noCache =
  Writers.setHeader "Cache-Control" "no-cache, no-store, must-revalidate"
  >>= Writers.setHeader "Pragma" "no-cache"
  >>= Writers.setHeader "Expires" "0"

let app =
  choose
    [ path "/" >>= Writers.setMimeType "text/html" >>= OK index
      GET >>= pathScan "/tweets/%s"  (fun id -> noCache >>= getMessages (int64 id))
      Files.browseHome ]
