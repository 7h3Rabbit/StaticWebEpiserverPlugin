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
  
## What functionality is provided in Plugin? ##

Below you can read more what StaticWebEpiserverPlugin can do today.
If you are missing something or something is not working as expected, please let me know :)

### Generate static pages on publishing of Pages and Blocks ###

StaticWebEpiserverPlugin uses the PublishedContent event on IContentEvents to listen for changes that are published on your site.
This way it will make sure your site is always up to date with what you have published in EpiServer.
No need for busting caches or having your website running without cache do always show the lastest information.
As long as your pages are inheriting from PageData and your blocks are inheriting from BlockData, StaticWebEpiserver will keep track of your changes.

### Generate static pages on running scheduled job ###

StaticWebEpiserverPlugin are also providing a scheduled job for you to run that will generate alla pages below the home of your website.
Pages that are not a child (or a child of a child, and so on) of your startpage it will not be included in the pages generated.
To start the job, run the job called "Generate StaticWeb".

### Having different views for StaticWeb and normal users ###

StaticWeb is registering a displaychannel called "StaticWeb" (See [Header.cshtml](https://github.com/7h3Rabbit/EpiServerStaticWebExample/blob/master/EpiserverAlloy/Views/Shared/Header.cshtml) and [Header.staticweb.cshtml](https://github.com/7h3Rabbit/EpiServerStaticWebExample/blob/master/EpiserverAlloy/Views/Shared/Header.staticweb.cshtml) for examples on how to use it, can be found in [EpiServerStaticWebExample](https://github.com/7h3Rabbit/EpiServerStaticWebExample/) repository). It is perfect for removing functionality that can't be used in a static website (like filitering or search). It also makes it possible for you to view how the page will look and work on the static version.

### How to ignore page or block type? ###
By inherit from `IStaticWebIgnoreGenerate` iterface on a page or block type you will tell StaticWeb NOT to generate a static version of this type when publishing or running the scheduled job.

### How to ignore page at runtime? ###
By inherit from `IStaticWebIgnoreGenerateDynamically` iterface on a page type you will tell StaticWebEpiServerPlugin that it MAY or MAY NOT generate a static version of this page when publishing or running the scheduled job.
StaticWebEpiServerPlugin will call method `ShouldGenerate` for the page and if it returns true, it will generate a static version of the page when publishing or running the scheduled job.
BUT if it returns false, it will not generate page AND also check (by calling `ShouldDeleteGenerated`) if it should remove any previously generated version of this page.
See [StandardPage.cs](https://github.com/7h3Rabbit/EpiserverAlloyWithForms/blob/master/Models/Pages/StandardPage.cs) for example on how it can be used.

### You can customize it using Events ###
We want people to be able to modify the use after their own liking.
There for we support the use of events on the IStaticWebService.
They will be called in the order specified below.
You can read more on what is available at [Issue #2](https://github.com/7h3Rabbit/StaticWebEpiserverPlugin/issues/2)

#### Example: Using [RequiredCssOnly](https://github.com/7h3Rabbit/StaticWebEpiserverPlugin.RequiredCssOnly) extension ####
Showing how to extract only the required CSS need for rendering the page and injecting rulesets as inline CSS.
To decrease dependencies and make your webpage have a faster initial load time by using AfterEnsurePageResources event in this example: [StaticWebRequiredCssDemoInitialization](https://github.com/7h3Rabbit/EpiServerStaticWebExample/blob/master/EpiserverAlloy/Business/Initialization/StaticWebRequiredCssDemoInitialization.cs)

#### Example: Inject HTML before saving ####
Showing how AfterGetPageContent event can be consumed in this example: [StaticWebMessageInjectionDemoInitialization](https://github.com/7h3Rabbit/EpiserverAlloyWithForms/blob/master/Business/Initialization/StaticWebMessageInjectionDemoInitialization.cs)

### Find, download and generate resources ###

When generating a page, StaticWebEpiserverPlugin will find all client side resources required for the page to work, download them and store them in the output folder along with the pages.

### Following markup will searched for resources ###

- script tag (src attribute)
- link tag (href attribute)
- img tag (src attribute)
- source tag (srcset attribute)

### Following resource types will be stored ###
  - css (and resources declared in url())
  - javascript (no dependencies)
  - Web fonts (woff and woff2)
  - Images (png, jpg, jpeg, jpe, gif, webp)
  - documents (pdf)
  - Icons (ico)

The rest will be ignored.

## Requirements ##

- EpiServer 7.5+
- .Net 4.7.2+
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
- Add below into: `<configSections>` element of `web.config`
`<section name="staticweb" type="StaticWebEpiserverPlugin.Configuration.StaticWebConfigurationSection" />`
- Add below to the same level as `appSettings` element (for example right below the end tag)
- `<staticweb>`
    - `<sites>`
        - `<add`
                `name="Test Website"`
                `enabled="true/false"` (default: `true`)
                `url=""`
                `outoutPath=""`
                `resourceFolder=""`
                `useRouting="true/false"`  (default: `false`)
                `useHash="true/false"` (default: `true`)
                `useResourceUrl="true/false"` (default: `false`)
                ` />`
`	<staticweb>
		<sites>
			<add name="ExampleSite1" url="http://localhost:49822/" outputPath="C:\code\Test\A" resourceFolder="cache\v1"/>
		</sites>
	</staticweb>
`

into the: `<configSections>` element of `web.config`.




- added new property `StaticWeb:OutputFolder` to appSettings section in Web.config (for example a GitHub repository folder). Example: `<add key="StaticWeb:OutputFolder" value="C:\inetpub\wwwroot" />`
- added new property `StaticWeb:InputUrl` to appSettings section in Web.config (must allow anonymous access). Example: `<add key="StaticWeb:InputUrl" value="http://localhost:49822/" />`
- You are ready to go :)


By doing so you unlock the use of StaticWeb configuration section:
`	<staticweb>
		<sites>
			<add name="Example website1" url="http://localhost:49822/" outputPath="C:\code\Test\A" resourceFolder="cache\v1"/>
		</sites>
	</staticweb>
`

## Site Configuration ##

- `<staticweb>`
    - `<sites>`
        - `<add`
                `name=""`
                `enabled="true/false"` (default: `true`)
                `url=""`
                `outputPath=""`
                `resourceFolder=""`
                `useRouting="true/false"`  (default: `false`)
                `useHash="true/false"` (default: `true`)
                `useResourceUrl="true/false"` (default: `false`)
                ` />`
                
### `<add name="Example Site 1"` (default: ``) ###
Specifies a name for your website.
If specified it will be used in Scheduled job status information.
By default this is will use EpiServer site name if used.

### `<add enabled="true/false"` (default: `true`) ###
Allows you to disable plugin by just changing configuration.
Good if you temporarly want to disable plugin or if you on one server want to disable functionality (for example on editor only servers).

### `<add url="http://localhost:49822/"` (_required_) ###
Specifies url to use as base url for this website.
Make sure it matches one specified in EpiServer under `Config` -> `Manage Websites.

### `<add outputPath="C:\websites\website1\wwwroot"` (_required_) ###
Folder to write your static website to.
This can be any folder you have read, write and change access to.
For example a GitHub repository folder, folder used directly by IIS or directly into your EpiServer website (look more at: `userRouting`).

### `<add resourceFolder="cache\v1"` (default: ``) ###
Sub folder of `outputPath` to write resources to.
Above tells StaticWebEpiServerPlugin to use a subfolder `cache\v1` for all resources.
You should also look at: `useHash` and `useResourceUrl`.

### `<add useRouting="true/false"` (default: `false`) ###
By setting this to "true" you allow StaticWebEpiServerPlugin to write pages and resources inside a EpiServer instance and taking over the routing for pages it has generated static html pages, returning them instead of calling the page controllers.
Relative resource path needs to be set also to use this (read more on `useResourceUrl`).

### `<add useHash="true/false"` (default: `true`) ###
Tells StaticWebEpiServerPlugin to generate content hash and use for resource name.
_( This value has to be true to support .axd resources and make them static)_

### `<add useResourceUrl="true/false"` (default: `false`) ###
Tells StaticWebEpiServerPlugin to use orginal resource url for resource name.
_(If you also set `useHash` to true it will combine the two)_

