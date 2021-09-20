#r "nuget: Akka.FSharp, 1.4.25"
#time "on"
open System
open System.Security.Cryptography
open System.Text
open Akka.Actor
open Akka.Configuration
open Akka.FSharp

let stopWatch = System.Diagnostics.Stopwatch.StartNew()
Console.WriteLine("Please enter the number of leading zeroes:")
let lead = int(Console.ReadLine())
let gator = "tanishq.shaikh"

let genRandomNumbers count =
    let rnd = System.Random()
    List.init count (fun _ -> rnd.Next ())

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
let mutable CurrentCount = 0
let system = ActorSystem.Create("CoinMiner")

type TestMessage = 
    | WorkerMessage of int*int
    | EndMessage of string
    | SupervisorMessage of int

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
                                            
                                            
        | _ -> printfn "Erraneous Message!"
        
    }
    loop ()
    

let CoinSupervisor (mailbox:Actor<_>) =
    let rec loop()=actor{
        let! message = mailbox.Receive()
        match message with 
        | SupervisorMessage(lead) -> let listOfWorkers = [for i in 1 .. workerCount do spawn system ("Actor" + string(i) ) CoinWorker  ]
                                    //  let flag = 1
                                     Console.WriteLine(listOfWorkers.Item(0))
                                     for i in 0 .. workerCount-1 do //distributing work to the workers
                                        printfn "Worker %i " i 
                                        listOfWorkers.Item(i) <! WorkerMessage(1,lead)
                                        // listOfWorkers.Item(i).Ask(EndMessage) |> ignore
                                        // mailbox.Context.Stop(listOfWorkers.Item(i))
                                        // listOfWorkers.Item(i) <! PoisonPill.Instance
                                     

        | EndMessage(textMsg) ->   if textMsg = "Done" then
                                    printfn "%s" textMsg
                                    mailbox.Context.System.Terminate() |> ignore
        | _ -> printfn "Erraneous Message!"
    }
    loop()

 
let CoinSupervisorRef = spawn system "CoinSupervisor" CoinSupervisor

CoinSupervisorRef <! SupervisorMessage(lead)
// CoinSupervisorRef <! PoisonPill.Instance
#time "on"
// system.WhenTerminated.Wait()
system.Terminate()

// stopWatch.Stop()
// printfn "%f" stopWatch.Elapsed.TotalMilliseconds