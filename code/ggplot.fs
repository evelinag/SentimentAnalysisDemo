module FFplot

open Deedle
open RProvider
open RProvider.``base``
open RProvider.ggplot2

let (++) (plot1:RDotNet.SymbolicExpression) (plot2:RDotNet.SymbolicExpression) = 
    R.``+``(plot1, plot2)

/// Wrapper around common ways of initialising a ggplot object
type G =
    static member ggplot() = R.ggplot()

    static member ggplot(dataframe:RDotNet.SymbolicExpression, ?aes:RDotNet.SymbolicExpression) = 
        let parameters = 
            ("data", box dataframe)::
            (match aes with
             | None -> []
             | Some a -> ["mapping", box a])

        namedParams parameters
        |> R.ggplot

    static member ggplot(data:Frame<_,_>, ?aes:RDotNet.SymbolicExpression) = 
        let df = R.as_data_frame(data)
        match aes with
        | None -> G.ggplot(df)
        | Some a -> G.ggplot(df, a)

    /// colour in aes is specified by data column name
    static member aes(x:string, ?y:string, ?colour:string, ?fill:string) = 
        let parameters = 
            [ Some ("x", x)
              (match y with | Some value -> Some("y", value) | None -> None)
              (match colour with | Some c -> Some("colour", c) | None -> None)
              (match fill with | Some c -> Some("fill", c) | None -> None) ]
            |> List.choose id
        R.aes__string(namedParams parameters)
 
