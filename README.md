# windows-dns-api

## Purpose

This is a small API project, meant to run as a Windows Service, that uses Powershell to manage Windows DNS.  Generally, it can be run on any of Windows DNS servers.

## Installing the Service

You can install this service using any of the methods described in [Host ASP.NET Core in a Windows Service](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service?view=aspnetcore-7.0&tabs=visual-studio).  The service will need to run as a user with Powershell access and rights to modify the VMs.  

You will most likely have to create a new user and grant them service log on via local security policy.  See [Enable Service Logon](https://learn.microsoft.com/en-us/system-center/scsm/enable-service-log-on-sm?view=sc-sm-2022) for details.

### Deployment Powershell

Once the service is installed, Deploy-ToMachine.ps1 can be used to stop the service on the target machine, build this library in debug, and copy the build to the machine.  This is meant to be a template for your own deployments.

## Authentication

This API is setup with optional JWT Bearer Token authentication.  If you have an OAuth 2 service, you can configure authentication by setting the `Identity` values in the `appSettings.json` folder.

| Identity Property | Description                                               |
| ----------------- | --------------------------------------------------------- |
| AuthorityUrl      | The url of the Token Authority                            |
| ApiName           | This is the audience value required for the incoming call |

At this time, only the audience is validated via the ApiName.  Further validation, such as issuer and issuer signing key, can be added to the code if you need.  [How to Implement JWT Authentication in ASP.NET Core 6](https://www.infoworld.com/article/3669188/how-to-implement-jwt-authentication-in-aspnet-core-6.html) will get you started.
