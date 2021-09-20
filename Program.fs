// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open System.IO
open System.Security.Cryptography

// Define a function to construct a message to print
// let from whom =
//     sprintf "from %s" whom

// [<EntryPoint>]
for arg in fsi.CommandLineArgs |> Seq.skip 1 do
    printf "Calculating sha256 of %s\n  " arg
    File.ReadAllBytes(arg) |> (new SHA256Managed()).ComputeHash |> Seq.iter (printf "%x")
    printfn ""
