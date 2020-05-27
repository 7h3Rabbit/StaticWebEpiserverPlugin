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
- Supports having some pages dynamic and others generated static (see `UseRouting` below)
- Supports custom resource types (You can extend default or start from scratch using [`<allowedResourceTypes>`](#allowedresourcetypes-configuration))

**Con/limitations**

- Only pages inheriting from PageData will trigger page write
- Only block inheriting from BlockBata will trigger page write
- Resource limitations:
  - css (only support dependencies declared in url())
  - javascript (no dependencies)
  
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

- script element (src attribute)
- link element (href attribute)
- a element (href attribute)
- img element (src attribute)
- source element (srcset attribute)

### Following resource types will be stored by default ###
  - css (and resources declared in url())
  - javascript (no dependencies)
  - Web fonts (woff and woff2)
  - Images (png, jpg, jpeg, jpe, gif, webp, svg)
  - documents (pdf)
  - Icons (ico)
  - Assembly Resources (WebResource.axd and ScriptResource.axd as long as resulting content type are allowed)

The rest will be ignored.

(Note: You can add or change supported resource types using `<allowedResourceTypes>`)

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
                `url="http://localhost:49822/"`
                `outputPath="C:\websites\website1\wwwroot"`
                `resourceFolder="cache\v1"`
                ` />`
		
- Change `url` and `outputPath` after your needs and you are ready to go :)


## Site Configuration ##

With the `<sites>` configuration you can define multiple websites and also have different settings between them.


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
		
Below you can see the different attributes that can be used for a site.
                
### `Name="Example Site 1"` (default: ``) ###
Specifies a name for your website.
If specified it will be used in Scheduled job status information.
By default this is will use EpiServer site name if used.

### `Enabled="true/false"` (default: `true`) ###
Allows you to disable plugin by just changing configuration.
Good if you temporarly want to disable plugin or if you on one server want to disable functionality (for example on editor only servers).

### `Url="http://localhost:49822/"` (_required_) ###
Specifies url to use as base url for this website.
Make sure it matches one specified in EpiServer under `Config` -> `Manage Websites.

### `OutputPath="C:\websites\website1\wwwroot"` (_required_) ###
Folder to write your static website to.
This can be any folder you have read, write and change access to.
For example a GitHub repository folder, folder used directly by IIS or directly into your EpiServer website (look more at: `userRouting`).

### `ResourceFolder="cache\v1"` (default: ``) ###
Sub folder of `outputPath` to write resources to.
Above tells StaticWebEpiServerPlugin to use a subfolder `cache\v1` for all resources.
You should also look at: `useHash` and `useResourceUrl`.

### `UseRouting="true/false"` (default: `false`) ###
By setting this to "true" you allow StaticWebEpiServerPlugin to write pages and resources inside a EpiServer instance and taking over the routing for pages it has generated static html pages, returning them instead of calling the page controllers.
Relative resource path needs to be set also to use this (read more on `useResourceUrl`).

### `UseHash="true/false"` (default: `true`) ###
Tells StaticWebEpiServerPlugin to generate content hash and use for resource name.
_( This value has to be true to support .axd resources and make them static)_

### `UseResourceUrl="true/false"` (default: `false`) ###
Tells StaticWebEpiServerPlugin to use orginal resource url for resource name.
_(If you also set `useHash` to true it will combine the two)_


## AllowedResourceTypes Configuration ##
With the `<allowedResourceTypes>` you can add, remove or change support for resource types.
This is usefull if you for example want to extend support for more file extensions.

The `<allowedResourceTypes>` should be a direct child element to the `<staticweb>` element in your web.config.
Below you will find how it can be used.

### Add/extend support for resource type ###
Below illustrate how to extend the default resource types that are being supported by adding support for the .mp4 file extension and bind it to the video/mp4 mime type.

	`<allowedResourceTypes>
		<add fileExtension=".mp4" mimeType="video/mp4" />
	</allowedResourceTypes>`

### Define your own list of allowed resource types ###
Below illustrate how to remove the default resource types and start from scratch.
Allowing you to finetune exactly what resource types you support.
In below example we only support resources with the following file extensions (and resources using mime type related to that): `.css`, `.js` and `.jpg`

	`<allowedResourceTypes>
		<clear />
		<add fileExtension=".css" mimeType="text/css" />
		<add fileExtension=".js" mimeType="text/javascript" />
		<add fileExtension=".jpg" mimeType="image/jpg" />
	</allowedResourceTypes>`
