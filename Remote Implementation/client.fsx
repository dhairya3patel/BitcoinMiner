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

let addr = "akka.tcp://RemoteCoinMiner@"+server+":9090/user/myServer"
let mutable workerCount=System.Environment.ProcessorCount|> int

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
        }")

let system = ActorSystem.Create("ClientCoinMiner", configuration)

type TestMessage = 
    | WorkerMessage of int*int
    | EndMessage of string
    | SupervisorMessage of int

Console.WriteLine("Please enter the number of leading zeroes:")
let lead = int(Console.ReadLine())
let gator = "dhairya.patel"

let genRandomNumbers count =
    let rnd = System.Random()
    List.init count (fun _ -> rnd.Next (5,10))

let ranStr n = 
    let r = Random()
    let chars = Array.concat([[|'a' .. 'z'|];[|'A' .. 'Z'|];[|'0' .. '9'|]])
    let sz = Array.length chars in
    String(Array.init n (fun _ -> chars.[r.Next sz]))

let GetHash gator nonce suffix :string = 

    let sha = System.Security.Cryptography.SHA256.Create()

    let var = gator + ";" + suffix + nonce.ToString()
    let hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(var))).Replace("-", "")

    hash

let FindCoin gator lead =
    let length = genRandomNumbers 1
    let suffix = ranStr length.Head    
    let mutable verifier = "0"
    let mutable i = 1
    while i<lead do
        verifier <- verifier + "0"
        i <- i + 1
    let mutable nonce = 0
    let mutable x = true
    let mutable coin = ""
    while x do
        let mutable hash = GetHash gator nonce suffix
        if hash.StartsWith(verifier) then 
            x <- false
            coin <- gator+";"+suffix+nonce.ToString()+"\t"+hash
        else
            coin <- "New"    
        nonce <- nonce + 1 
    coin.ToString()
    

let CoinWorker (mailbox: Actor<_>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive()
        match message with
        | WorkerMessage(first, last) ->     let coin = FindCoin gator last
                                            let echoClient = system.ActorSelection(addr)
                                            let msgToServer = coin.ToString()
                                            echoClient <! "Remote" + msgToServer
                                            let sender = mailbox.Sender()
                                            sender <! EndMessage("Done")
                                            
                                            
        | _ -> printfn "Erraneous Message!"
        
    }
    loop ()
    

let CoinSupervisor (mailbox:Actor<_>) =
    let rec loop()=actor{
        let! message = mailbox.Receive()
        match message with 
        | SupervisorMessage(lead) -> let listOfWorkers = [for i in 1 .. workerCount do spawn system ("Actor" + string(i) ) CoinWorker  ]
                                     Console.WriteLine(listOfWorkers.Item(0))
                                     for i in 0 .. workerCount-1 do //distributing work to the workers
                                        printfn "Worker %i " i 
                                        listOfWorkers.Item(i) <! WorkerMessage(1,lead)
                                     

        | EndMessage(textMsg) ->   if textMsg = "Done" then
                                    printfn "%s" textMsg
                                    mailbox.Context.System.Terminate() |> ignore
        | _ -> printfn "Erraneous Message!"
    }
    loop()

let mutable remoteWorkDone = false
let commlink = 
    spawn system "client"
    <| fun mailbox ->
        let rec loop() =
            actor {
                let! msg = mailbox.Receive()
                printfn "%s" msg 
                let response =msg|>string
                let command = (response).Split ','
                if command.[0].CompareTo("init")=0 then
                    let echoClient = system.ActorSelection(addr)
                    let msgToServer = "Starting"
                    echoClient <! msgToServer
                    let CoinSupervisorRef = spawn system "CoinSupervisor" CoinSupervisor
                    CoinSupervisorRef <! SupervisorMessage(lead)
                    echoClient <! SupervisorMessage(lead)
                elif response.CompareTo("ProcessingDone")=0 then
                    system.Terminate() |> ignore
                    remoteWorkDone <- true
                else
                    printfn "-%s-" msg

                return! loop() 
            }
        loop()



commlink <! "init"
system.WhenTerminated.Wait()