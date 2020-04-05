# StaticWebEpiserverPlugin
Generate static website but still use EpiServer as your CMS for your editors

## Introduction ##

Do you need a crisis web or handling peek load of users on your site?
I have started develop a add-on for sites using EpiServer that will take everything you publish and make a static version of it.
This way you get the instant publishing of a dynamic website and the performance and scalability of static websites in one go.
It should (havn't yet test it) be possible to create the pages directory in your EpiServer site making it fully dynamic for things like search and filtering but static for general information.

**Pro**

- Reliable serverside response time
- Very easy to scale up
- No database dependency for visitor
- No serverside code requried
- Very secure (hard to hack static pages)

**Con/limitations**

- No serverside dynamic content can be used
- No serverside personalized content can be used
- Only pages inheriting from PageData will trigger page write
- Only block inheriting from BlockBata will trigger page write
- Only supports following types:
  - css (only support dependencies declared in url())
  - javascript (no dependencies)
  - Web fonts (woff and woff2)
  - Images (png, jpg, jpeg, jpe, gif, webp)
  - documents (pdf)
  - Icons (ico)

## Requirements ##

- EpiServer 7.5+
- .Net 4.7+
- All pages need to inherit from PageData
- All blocks needs to inherit from BlockData
- Website has to return pages, javascript and css as UTF-8
- Must allow visits with user-agent `Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.149 Safari/537.36 StaticWebPlugin/0.1`


## Installation ##

### NuGet ###
- Add nuget package https://www.nuget.org/packages/StaticWebEpiserverPlugin/ to your solution.
- added new property `StaticWeb:OutputFolder` to appSettings section in Web.config (for example a GitHub repository folder). Example: `<add key="StaticWeb:OutputFolder" value="C:\inetpub\wwwroot" />`
- added new property `StaticWeb:InputUrl` to appSettings section in Web.config (must allow anonymous access). Example: `<add key="StaticWeb:InputUrl" value="http://localhost:49822/" />`
- You are ready to go :)

### GitHub Source download ###
- Copy `StaticWebEpiserverPlugin` folder and add `StaticWebEpiserverPlugin.csproj` into your solution.
- added new property `StaticWeb:OutputFolder` to appSettings section in Web.config (for example a GitHub repository folder). Example: `<add key="StaticWeb:OutputFolder" value="C:\inetpub\wwwroot" />`
- added new property `StaticWeb:InputUrl` to appSettings section in Web.config (must allow anonymous access). Example: `<add key="StaticWeb:InputUrl" value="http://localhost:49822/" />`
- You are ready to go :)





## What functionality is provided in Plugin? ##

- Do changes for a page or block(must be placed on a page) in EpiServer and publish your changes.
- StaticWebPlugin will now try to access your website and write back the result it gets to the folder you have entered.

### Generate static pages on publishing of Pages and Blocks ###

TBD

### Generate static pages on running scheduled job ###

TBD

### Having different views for StaticWeb and normal users ###

StaticWeb is registering a displaychannel called "StaticWeb" (See `Header.cshtml` and `Header.staticweb.cshtml` for examples on how to use it). It is perfect for removing functionality that can't be used in a static website (like filitering or search). It also makes it possible for you to view how the page will look and work on the static version.

### Find, download and generate resources ###

TBD
