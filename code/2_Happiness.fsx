#load @"packages/FsLab/FsLab.fsx"

#I "packages/Stanford.NLP.Parser/lib/"
#I "packages/Stanford.NLP.CoreNLP/lib/"
#I "packages/IKVM/lib/"
#r "IKVM.OpenJDK.Core.dll"
#r "IKVM.OpenJDK.Util.dll"
#r "stanford-parser.dll"
#r "stanford-corenlp-3.5.2.dll"
#r "ejml-0.23.dll"

open System
open System.IO
open FSharp.Data
open XPlot.GoogleCharts

open java.io
open java.util
open edu.stanford.nlp.``process``
open edu.stanford.nlp.ling
open edu.stanford.nlp.trees
open edu.stanford.nlp.parser.lexparser
open edu.stanford.nlp.pipeline
open edu.stanford.nlp.util
open edu.stanford.nlp.io
open edu.stanford.nlp.trees
open edu.stanford.nlp.semgraph
open edu.stanford.nlp.sentiment
open edu.stanford.nlp.neural
open edu.stanford.nlp.neural.rnn

let jarDirectory =
    __SOURCE_DIRECTORY__
    + @"/stanford-parser-full-2015-01-30/"

// Annotation pipeline configuration
let props = Properties()
props.setProperty("annotators",
    "tokenize, ssplit, pos, parse, sentiment") |> ignore
props.setProperty("sutime.binders","0") |> ignore

// Create the pipeline
Directory.SetCurrentDirectory(jarDirectory)
let pipeline = StanfordCoreNLP(props)

let getSentimentMeaning value =
    match value with
    | 0 -> "Negative"
    | 1 -> "Somewhat negative"
    | 2 -> "Neutral"
    | 3 -> "Somewhat positive"
    | 4 -> "Positive"

let evaluateSentiment (text:string) =
    // Annotation
    let annotation = Annotation(text)
    pipeline.annotate(annotation)

    let sentences = annotation.get(CoreAnnotations.SentencesAnnotation().getClass()) :?> java.util.ArrayList

    let sentiments =
        [ for s in sentences ->
            let sentence = s :?> Annotation
            let sentenceTree = sentence.get(SentimentCoreAnnotations.SentimentAnnotatedTree().getClass()) :?> Tree
            let sentiment = RNNCoreAnnotations.getPredictedClass(sentenceTree)
            let preds = RNNCoreAnnotations.getPredictions(sentenceTree)
            let probs = [ for i in 0..4 -> preds.get(i)]
            sentiment |> getSentimentMeaning, probs ]

    printfn "%s" (fst sentiments.[0])

evaluateSentiment "The movie was funny."
evaluateSentiment "The movie was trying to be funny."

evaluateSentiment "I'm at a software developer conference."

// Preprocessed data
//====================================

let [<Literal>] filename = __SOURCE_DIRECTORY__ + "/data/sentiments.json"

type SentimentFile = JsonProvider<filename>
let stweets = SentimentFile.Load(filename)

for tweet in stweets.[0..100] do
    printfn "%s : %s [%A]" tweet.User tweet.Text (tweet.Sentiment |> Seq.averageBy (fun s -> float s.Overall) )

let justSentiments =
    [| for it in 0..stweets.Length-1 do
        for s in stweets.[it].Sentiment -> s.Overall |]

justSentiments
|> Seq.countBy id
|> Chart.Bar

evaluateSentiment "I'm at a software developer conference."
evaluateSentiment "I am using csharp for programming."

// Look at positive and negative tweets

let tweetsByValue idx =
    stweets
    |> Array.filter (fun t -> t.Text.Contains "RT" |> not)
    |> Array.choose (fun t ->
        let s = t.Sentiment |> Array.map (fun x -> x.Overall)
        match (Array.tryFind ((=) idx) s) with
        | Some(_) -> Some(t)
        | None -> None)

let positiveTweets = tweetsByValue 4
let negativeTweets = tweetsByValue 0

let rnd = System.Random()
let giveMePositiveTweet () =
    let t = positiveTweets.[rnd.Next(positiveTweets.Length)]
    t.User, t.Text

let giveMeNegativeTweet() =
    let t = negativeTweets.[rnd.Next(negativeTweets.Length)]
    t.User, t.Text

giveMePositiveTweet()
giveMeNegativeTweet()

positiveTweets.[32].Text


