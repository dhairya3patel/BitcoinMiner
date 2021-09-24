#r "nuget: Akka.FSharp"

open System
open Akka.Actor
open Akka.FSharp
open System.Security.Cryptography
open System.Text


Console.WriteLine("Enter the number of leading zeroes:")
let lead = int (Console.ReadLine())
let gator = "dhairya.patel"

let genlength: int =
    let r = Random()
    let li = List.init 1 (fun _ -> r.Next(5, 10))
    li.Head

let seedStr len : string =
    let r = Random()

    let chars =
        Array.concat (
            [ [| 'a' .. 'z' |]
              [| 'A' .. 'Z' |]
              [| '0' .. '9' |] ]
        )

    let str = Array.length chars in
    String(Array.init len (fun _ -> chars.[r.Next str]))

let GetHash gator nonce suffix : string =

    let sha = new SHA256Managed()
    let var = gator + ";" + suffix + nonce.ToString()

    let hashB =
        sha.ComputeHash(Encoding.ASCII.GetBytes(var)) //.Replace("-", "")

    let hashS =
        hashB
        |> Array.map (fun (x: byte) -> String.Format("{0:X2}", x))
        |> String.concat String.Empty

    hashS

//Actor-model
let workerCount = Environment.ProcessorCount

let system =
    ActorSystem.Create("CoinMiner")

type CommunicationMessages =
    | WorkerMessage of int * int
    | EndMessage of string
    | SupervisorMessage of int
    | CoinMessage of string

let FindCoin gator lead =
    let length = genlength
    let suffix = seedStr length
    let mutable verifier = "0"
    let mutable i = 1

    while i < lead do
        verifier <- verifier + "0"
        i <- i + 1

    let mutable nonce = 0
    let mutable x = true
    let mutable coin = ""

    while x do
        let mutable hash = GetHash gator nonce suffix

        if hash.StartsWith(verifier) then
            x <- false

            coin <-
                gator
                + ";"
                + suffix
                + nonce.ToString()
                + "\t"
                + hash
        else
            coin <- "New"

        nonce <- nonce + 1

    coin |> ignore
    Console.WriteLine(coin)




let CoinWorker (mailbox: Actor<_>) =
    let rec loop () =
        actor {
            let! message = mailbox.Receive()

            match message with
            | WorkerMessage (first, last) ->
                FindCoin gator last
                let sender = mailbox.Sender()
                sender <! EndMessage("Done")


            | _ -> printfn "Erraneous Message from the Supervisor! "

        }

    loop ()


let CoinSupervisor (mailbox: Actor<_>) =
    let rec loop () =
        actor {
            let! message = mailbox.Receive()

            match message with
            | SupervisorMessage (lead) ->
                let listOfWorkers =
                    [ for i in 1 .. workerCount do
                          yield (spawn system ("LocalActor" + string (i))) CoinWorker ]

                for i in 0 .. workerCount - 1 do //distributing work to the workers
                    // printfn "Worker %i " i
                    listOfWorkers.Item(i) <! WorkerMessage(1, lead)
            // listOfWorkers.Item(i).Ask(EndMessage) |> ignore
            // mailbox.Context.Stop(listOfWorkers.Item(i))
            // listOfWorkers.Item(i) <! PoisonPill.Instance


            | CoinMessage (coin) -> printfn "%s" coin
            //    mailbox.Context.System.Terminate() |> ignore

            | EndMessage (textMsg) ->
                if textMsg = "Done" then
                    mailbox.Context.System.Terminate() |> ignore
            | _ -> printfn "Erraneous Message!"

            return! loop ()
        }

    loop ()


let CoinSupervisorRef =
    spawn system "CoinSupervisor" CoinSupervisor


CoinSupervisorRef <! SupervisorMessage(lead)
#time "on"
system.WhenTerminated.Wait()
