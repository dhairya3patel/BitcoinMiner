#r "nuget: Akka.FSharp"
#r "nuget: Akka.Remote"
open Akka.Actor
open Akka.Configuration
open Akka.FSharp

let system = ActorSystem.Create("Remote_Miner")