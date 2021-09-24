#time "on"
#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"

open System
open Akka.Actor
open Akka.Configuration
open Akka.FSharp
open System
open System.Security.Cryptography
open System.Text

let server = fsi.CommandLineArgs.[1] |> string
let mutable coinCount = 0
let mutable maxcoincapactiy = 0
let mutable verifier = "0"

let addr =
    "akka.tcp://RemoteCoinMiner@"
    + server
    + ":9090/user/myServer"

let mutable workerCount = Environment.ProcessorCount |> int

let configuration =
    ConfigurationFactory.ParseString(
        @"akka {
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""

            }
            remote {
                helios.tcp {
                    port = 0
                    hostname = localhost
                }
            }
        }"
    )

let system =
    ActorSystem.Create("ClientCoinMiner", configuration)

type CommunicationMessages =
    | WorkerMessage of int * IActorRef
    | EndMessage of IActorRef * string
    | SupervisorMessage of int
    | CoinMessage of string

// Console.WriteLine("Please enter the number of leading zeroes:")
// let lead = int (Console.ReadLine())
let gator = "dhairya.patel"


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


let FindCoin gator length =
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

    coin.ToString()


let CoinWorker (mailbox: Actor<_>) =
    let rec loop () =
        actor {
            let! message = mailbox.Receive()

            match message with
            | WorkerMessage (length, workerAddress) ->
                let coin = FindCoin gator length
                let serverActor = system.ActorSelection(addr)
                let sendServer = coin.ToString()
                serverActor <! "Remote: " + sendServer
                let sender = mailbox.Sender()
                sender <! EndMessage(workerAddress, coin) //"Done")


            | _ -> printfn "Erraneous Message!"

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
                          spawn system ("RemoteActor" + string (i)) CoinWorker ]
                //  Console.WriteLine(listOfWorkers.Item(0))
                for i in 0 .. workerCount - 1 do //distributing work to the workers
                    // printfn "Worker %i " i
                    listOfWorkers.Item(i)
                    <! WorkerMessage(7, listOfWorkers.Item(i))


            | EndMessage (workerAddress, returnedCoin) ->
                coinCount <- coinCount + 1

                if coinCount = maxcoincapactiy then
                    system.Terminate() |> ignore
                else
                    workerAddress <! WorkerMessage(7, workerAddress)
            | _ -> printfn "Erraneous Message!"
        }

    loop ()

let mutable remoteWorkDone = false

let Network =
    spawn system "ClientMiner"
    <| fun mailbox ->
        let rec loop () =
            actor {
                let! message = mailbox.Receive()
                printfn "%s" message
                let resp = message |> string
                let order = (resp).Split ','
                let serverActor = system.ActorSelection(addr)

                if order.[0].CompareTo("Start") = 0 then
                    let sendServer = "Starting"
                    serverActor <! sendServer
                elif order.[0].CompareTo("CoinCapacity") = 0 then
                    let lead = order.[1] |> int
                    let mutable i = 1

                    while i < lead do
                        verifier <- verifier + "0"
                        i <- i + 1

                    maxcoincapactiy <- order.[2] |> int

                    let CoinSupervisorRef =
                        spawn system "CoinSupervisor" CoinSupervisor

                    CoinSupervisorRef <! SupervisorMessage(lead)
                    serverActor <! SupervisorMessage(lead)
                elif resp.CompareTo("END") = 0 then
                    printfn "-%s-" message
                    system.Terminate() |> ignore
                else
                    printfn "-%s-" message

                return! loop ()
            }

        loop ()



Network <! "Start"
system.WhenTerminated.Wait()
