module App

open Sutil
open Sutil.DOM
open Sutil.Attr

let view () =
  let store = Store.make true

  Html.app [
    Html.main [
      Html.label [
        Html.input [
          type' "checkbox"
          Bind.attr ("checked", store)
        ]
        text "Show Text"
      ]
      Html.p [
        Bind.attr ("hidden", (store .> not))
        text "Hey there! this is some Fable stuff!"
      ]
    ]
  ]
