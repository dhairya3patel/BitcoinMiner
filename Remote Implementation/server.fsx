#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System.Security.Cryptography
open System.Text

let configuration =
    ConfigurationFactory.ParseString(
        @"akka {
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                debug : {
                    receive : on
                    autoreceive : on
                    lifecycle : on
                    event-stream : on
                    unhandled : on
                }
            }
            remote {
                helios.tcp {
                    port = 9090
                    hostname = 10.20.115.14
                }
            }
        }"
    )

Console.WriteLine("Enter the number of leading zeroes:")
let lead = int (Console.ReadLine())
let mutable verifier = "0"
let mutable i = 1

while i < lead do
    verifier <- verifier + "0"
    i <- i + 1

let gator = "dhairya.patel"
let mutable coinCount = 0
let mutable maxCoinCapacity = 16
let mutable localCount = 0

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
    ActorSystem.Create("RemoteCoinMiner", configuration)

type CommunicationMessages =
    | WorkerMessage of int * IActorRef
    | EndMessage of IActorRef * string
    | SupervisorMessage of int
    | CoinMessage of string

let FindCoin gator length =
    // let length = genlength
    let suffix = seedStr length
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

    coin




let CoinWorker (mailbox: Actor<_>) =
    let rec loop () =
        actor {
            let! message = mailbox.Receive()

            match message with
            | WorkerMessage (length, workerAddress) ->
                let returnedCoin = FindCoin gator length
                let sender = mailbox.Sender()
                sender <! EndMessage(workerAddress, returnedCoin)

            | _ -> printfn "Erraneous Message from the Supervisor! "

            return! loop ()
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
                    listOfWorkers.Item(i)
                    <! WorkerMessage(5, listOfWorkers.Item(i))
            | CoinMessage (coin) -> printfn "%s" coin

            | EndMessage (workerAddress, returnedCoin) ->
                printfn "%s" returnedCoin
                coinCount <- coinCount + 1

                if coinCount = maxCoinCapacity then
                    system.WhenTerminated.Wait() |> ignore
                else
                    workerAddress <! WorkerMessage(6, workerAddress)
            // WorkerMessage(1, lead)

            | _ -> printfn "Erraneous Message!"

            return! loop ()
        }

    loop ()


let CoinSupervisorRef =
    spawn system "CoinSupervisor" CoinSupervisor

let serverSetup =
    spawn system "myServer"
    <| fun mailbox ->
        let rec loop () =
            actor {
                let! msg = mailbox.Receive()
                printfn "%s" msg

                if msg = "Starting" then
                    maxCoinCapacity <- maxCoinCapacity / 2
                    printfn "%i" maxCoinCapacity

                    mailbox.Sender()
                    <! "CoinCapacity,"
                       + lead.ToString()
                       + ","
                       + maxCoinCapacity.ToString()
                    |> ignore
                // CoinSupervisorRef <! SupervisorMessage(lead)
                // else
                //     CoinSupervisorRef <! SupervisorMessage(lead)
                elif msg.Contains("Remote") then
                    localCount <- localCount + 1

                if localCount = maxCoinCapacity then
                    mailbox.Sender() <! "END"

                return! loop ()
            }

        loop ()

CoinSupervisorRef <! SupervisorMessage(lead)
serverSetup
#time "on"
system.WhenTerminated.Wait()
