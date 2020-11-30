# Conditional Bootstrap plugin for Sitecore Commerce

> This plugin was inspired by Guido van Tricht's ConditionalConfigs plugin (https://github.com/GuidovTricht/Sitecore.Commerce.Plugin.ConditionalConfigs)

This plugin adds conditional, rule based configuration for bootstrapping environment files. 

## How to use

When importing the json files from the environments folder, each file will be checked for a property named `$conditions`. You can put the `$conditions` property on any object node. Below is an example of the SQL Policy set that contains the connection configuration for the SharedEnvironments database in two different hosting environments: one is for development and one is for the test environment.

```
{
  "$type": "Sitecore.Commerce.Core.PolicySet, Sitecore.Commerce.Core",
  "Id": "Entity-PolicySet-SqlPolicySet",
  "Version": 1,
  "IsPersisted": false,
  "Name": "SqlPolicySet",
  "Policies": {
    "$type": "System.Collections.Generic.List`1[[Sitecore.Commerce.Core.Policy, Sitecore.Commerce.Core]], mscorlib",
    "$values": [
      {
        "$type": "Sitecore.Commerce.Plugin.SQL.EntityStoreSqlPolicy, Sitecore.Commerce.Plugin.SQL",
        "$conditions": {
          "HostingEnvironment": "dev"
        },
        "TrustedConnection": true,
        "UserName": "",
        "Password": "",
        "Server": ".",
        "Database": "SitecoreCommerce9_SharedEnvironments",
        "AdditionalParameters": "",
        "ConnectTimeout": 120000,
        "CommandTimeout": 180,
        "CleanEnvironmentCommandTimeout": 120000
      },
      {
        "$type": "Sitecore.Commerce.Plugin.SQL.EntityStoreSqlPolicy, Sitecore.Commerce.Plugin.SQL",
        "$conditions": {
          "HostingEnvironment": "tst"
        },
        "TrustedConnection": true,
        "UserName": "",
        "Password": "",
        "Server": "TestServer",
        "Database": "SitecoreCommerce9_SharedEnvironments",
        "AdditionalParameters": "",
        "ConnectTimeout": 120000,
        "CommandTimeout": 180,
        "CleanEnvironmentCommandTimeout": 120000
      }

    ]
  }
}

```

The `$conditions` property contains a dictionary where each key in the dictionary represents a setting in the `AppSettings` section of `config.json` and each value is the required value for the condition to be true.  The value can be a regular expression. 

When importing the policy json, the json will be checked for `$conditions` and for each node that has a condition, the plugin will check whether the conditions are true. If not, the node is *removed* from the json. 

For example, if your config.json looks like this:

```
{
  "AppSettings": {
    "HostingEnvironment": "dev",
    "SiteTitle": "Sitecore Commerce Engine",
    "BootStrapFile": "Global",
    .
    .
    .

}
```

only the policies that have:

```
        "$conditions": {
          "HostingEnvironment": "dev"
        },
```
will be imported. Note that any policies that don't have a `$conditions` entry, will also be imported.

