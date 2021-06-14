# Suave Dev Server Script

```sh
dotnet tool restore
dotnet fsi suave.fsx
```

once `suave.fsx` is running you can start fable by entering `start:fable` this will run fable in watch mode on the backgound


### Available commands

- start:fable
- restart:fable
- stop:fable

To quit entirely just do the usual `Ctrl+C`


## Key Points

This is all possible due to some updates in javascript and the browsers in the recent years, most notably [Javascript Modules](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Modules) which the Fable compiler emits so this is literally a no bundler, no transpiler, no node development solution.

There are a few things that can make this an annoying experience like any kind of transpilation phase (using sass, less, stylus among others) importing css (css modules are not yet in some browsers) and lastly this setup requires a modern browser (that at least supports JS Module imports) so you might still want to keep it with webpack for a while if any of those is something you want for the rest this might be something you want to check at

### Importing npm dependencies
Remember that there are CDN's like [unpkg](https://unpkg.com/) and [skypack](https://www.skypack.dev/) that can serve an npm package as a JS module so rather than importing your modules locally you would import them from the sky, I mean the cloud, I mean the internet