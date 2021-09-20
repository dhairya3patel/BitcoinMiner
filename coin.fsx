#time "on"
open System.Security.Cryptography
open System.Text
open System

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
    coin
let gator = "dhairya.patel"
let lead = int(Console.ReadLine()) 
let coin = FindCoin gator lead
Console.WriteLine coin