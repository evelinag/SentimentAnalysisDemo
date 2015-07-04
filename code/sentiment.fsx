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
            sentiment, probs ]

    fst sentiments.[0]
