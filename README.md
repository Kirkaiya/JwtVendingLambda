# JwtVendingLambda (.NET Core Lambda function)
**.NET Core 3.1**

A Proof of Concept Lambda function that vends JSON Web Tokens (JWTs) signed with a RSA private key,
and also generates the JWKS with the public key. 
These tokens can be used with AWS API Gateway HTTP API JWT Authorizers.

## Background

### Modernizing ASP.NET applications

Sometimes companies have legacy ASP.NET applications (typically built on .NET Framework on Windows),
that they want to modernize. These legacy applications - usually Web Forms, MVC or Razor pages - 
are often monoliths that use either Forms auth, or some homebrewed authentication mechanism
that ends up setting a cookie. This cookie is sent back and forth between the browser and the server.
Plenty of these applications are using ye olde 
[ASP.NET Membership](https://docs.microsoft.com/en-us/previous-versions/aspnet/yh26yfzy(v=vs.100)?redirectedfrom=MSDN)
 or the slightly-less old 
[ASP.NET Identity](https://docs.microsoft.com/en-us/aspnet/identity/overview/getting-started/)
framework. 

When developers start modernizing these apps, a common pattern is to extract a bit of functionality, 
port it to ASP.NET Core Web API, and extract the UI out as a SPA in React or Angular.
Once you move functionality into .NET Core (or .NET 5+), you can easily run it in Linux containers or
in serverless platforms like [AWS Lambda](https://aws.amazon.com/lambda/). You can then place a proxy 
in front to route traffic to the legacy or modernized services. In AWS, 
[API Gateway](https://aws.amazon.com/api-gateway/) and 
[Application Load Balancer (ALB)](https://aws.amazon.com/elasticloadbalancing/application-load-balancer/) 
are often used for this.

### Modernizing AuthN/AuthZ

Then there's the question of how to handle the authentication and authorization, and whether that's being
modernized as well. The desired end-state for authN/authZ is usually token-based auth, with an
identity provider like [AWS Cognito](https://aws.amazon.com/cognito/) validating credentials,
issuing JWT tokens, and storing the users with their attributes. The JWT tokens can be validated
either in your ASP.NET Core code (implementing the \[Authorize\] attribute) or offloaded completely 
to the proxy layer. One of the great features of API Gateway's HTTP APIs is the 
[built-in JWT Authorizer](https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-jwt-authorizer.html).
The JWT Authorizer validates JWT tokens using the public key serialized in the JWKS (JSON Web Key Set),
which is a JSON document containing one or more [JSON Web Keys (JWK)](https://tools.ietf.org/html/rfc7517).

Getting from the legacy ASP.NET Membership or Identity to the token-based auth using Cognito requires
some decisions. Some possibilities include:

 * Configure the new ASP.NET Core applications/microservices to share the auth cookie with the legacy app.
   * Pros: requires almost no changes to the legacy app, users can sign in via either app
   * Cons: the "modernized" app is still relying on older auth mechanism
   * AWS sample application using this approach: 
     [Github: dotnet-share-auth-cookie-between-monolith-and-modernized-apps](https://github.com/aws-samples/dotnet-share-auth-cookie-between-monolith-and-modernized-apps)
 * Modernize identity first - export all the users from the Membership/Identity tables to .CSV, then import
   into AWS Cognito, and update the legacy code to set the security principal from the JWT token being passed along by API Gateway, and update all the UI code to pass that JWT token back and forth.
   * Pros: guaranteed employement for however long that takes. Not much else.
   * Cons: rewriting tons of legacy code and UI, and lots of risk. And you can't do anything else till it's done.
 * Continue using the existing membership/identity in the legacy app, and use JWT tokens in the modernized apps.
    Add a bit of code to your legacy application to generate the JWT tokens (id token and access token), and 
    pass it back to the browser after initial authentication. Use that token for requests to the modernized APIs,
    which are fronted by API Gateway (which validates the tokens and forwards them to APIs).
   * Pros: the modernized app doesn't need to worry about authorizing users, and it won't require any changes
        when you eventually migrate to Cognito.
   * Cons: you would need to add the code to your legacy .NET Fx application to vend JWT tokens.

That last sub-bullet is where this proof-of-concept comes in.  Instead of adding the code to create
and sign JWT tokens to your 15-year-old ASP.NET application, you could just add the 
[AWSSDK.Lambda](https://www.nuget.org/packages/AWSSDK.Lambda/) NuGet package, and have the legacy 
application invoke this Lambda function. You can pass whatever claims you want (username, email,
phone number, roles, etc) and this function adds them as claims, and signs the JWT. You could even have multiple different
applications use the same Lambda function to generate tokens. Go crazy!

## Instructions to set it up:

### Create an RSA key pair
TBD

### Store the keys in AWS Secrets Manager
TBD

### Deploy and test the Lambda function
TBD
![screenshot of successful test in Lambda console](docs/console-test-succeeded.jpg)

### Host the JWKS.json and openid-configuration files in Amazon S3
TBD

### Create API Gateway HTTP API with JWT Authorizer
TBD

### Test it all out!
TBD
