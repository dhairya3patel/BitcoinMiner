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
                    hostname = 10.20.115.11
                }
            }
        }")

Console.WriteLine("Please enter the number of leading zeroes:")
let lead = int(Console.ReadLine())
let gator = "dhairya.patel"
// let mutable workerResponse = 0

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

//Actor-model
let workerCount = 8
// let system = ActorSystem.Create("CoinMiner")
let system = ActorSystem.Create("RemoteCoinMiner", configuration)

type CommunicationMessages = 
    | WorkerMessage of int*int
    | EndMessage of string
    | SupervisorMessage of int
    | CoinMessage of string

let FindCoin gator lead =
    let suffix = ranStr 5
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
    coin |> ignore
    Console.WriteLine(coin)
    
    


let CoinWorker (mailbox: Actor<_>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive()
        match message with
        | WorkerMessage(first, last) ->     FindCoin gator last
                                            let sender = mailbox.Sender()
                                            sender <! EndMessage("Done")
                                            
                                            
        | _ -> printfn "Erraneous Message from the Supervisor! "
        
    }
    loop ()
    

let CoinSupervisor (mailbox:Actor<_>) =
    let rec loop()=actor{
        let! message = mailbox.Receive()
        match message with 
        | SupervisorMessage(lead) -> let listOfWorkers = [for i in 1 .. workerCount do yield(spawn system ("Actor" + string(i) )) CoinWorker  ]
                                    //  let flag = 1
                                    //  Console.WriteLine(listOfWorkers)
                                     for i in 0 .. workerCount-1 do //distributing work to the workers
                                        printfn "Worker %i " i 
                                        listOfWorkers.Item(i) <! WorkerMessage(1,lead)
                                        // listOfWorkers.Item(i).Ask(EndMessage) |> ignore
                                        // mailbox.Context.Stop(listOfWorkers.Item(i))
                                        // listOfWorkers.Item(i) <! PoisonPill.Instance
                                     

        | CoinMessage(coin) -> printfn "%s" coin
                                        //    mailbox.Context.System.Terminate() |> ignore
            
        | EndMessage(textMsg) ->    if textMsg = "Done" then
                                        // printfn "%s" textMsg
                                    //     workerResponse <- workerResponse + 1
                                    // if workerResponse = workerCount then
                                        // mailbox.Context.System.WhenTerminated.Wait() |> ignore
                                        mailbox.Context.System.Terminate() |> ignore
        | _ -> printfn "Erraneous Message!"
        return! loop()
    }
    loop()

 
let CoinSupervisorRef = spawn system "CoinSupervisor" CoinSupervisor

let serverSetup = 
    spawn system "myServer"
        <| fun mailbox ->
            let rec loop()=actor{
                let! msg = mailbox.Receive()
                printfn "%s" msg 
                CoinSupervisorRef <! SupervisorMessage(lead)
                return! loop() 
            } loop()

// CoinSupervisorRef <! SupervisorMessage(lead)
serverSetup 
#time "on"
system.WhenTerminated.Wait()