# JwtVendingLambda (.NET Core Lambda function)
**.NET Core 3.1**

A Proof of Concept Lambda function that vends JSON Web Tokens (JWTs) signed with a RSA private key,
and also generates the JWKS with the public key. 
These tokens can be used with AWS API Gateway HTTP API JWT Authorizers.

## Background

Note - you don't need to read the background if you just want to test the project out.
Jump ahead down to the [instructions](#instructions) for that.

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

## <a name="instructions"/> Instructions to set it up:

### Create an RSA key pair

> *Note: if you already have an RSA keypair in base64 format, you can skip this.*

From a Windows 10 or Linux command prompt/terminal, use ssh-keygen to create a key pair. The code I used 
in the JwtBuilder class is hardcoded to RSA, but you can obviously change that.

**`ssh-keygen -t rsa`**

This prompts you to pick a filename, and to choose a passphrase. For just tesing out the JWT code,
you can leave the passphrase blank (just hit 'enter' and 'enter' again for the second prompt).
The response should look something like this (I ran this on Windows 10 and chose 'mynewrsakey' as the filename):

```
Generating public/private rsa key pair.
Enter file in which to save the key (C:\Users\myuser/.ssh/id_rsa): mynewrsakey
Enter passphrase (empty for no passphrase):
Enter same passphrase again:
Your identification has been saved in mynewrsakey.
Your public key has been saved in mynewrsakey.pub.
The key fingerprint is:
SHA256:QbqcIl2AKLTU3IusvmgLfZHLFFICOWvKirvFKdfBPwo ant\kirkdavi@PDX-07I62PC
The key's randomart image is:
+---[RSA 2048]----+
|o*+.+   .        |
|* o= o o         |
|.+o o + .        |
|.. * * o .       |
|+ o O + S        |
|.= * *           |
|* E = o          |
|=* o . .         |
|==o .            |
+----[SHA256]-----+
```

This creates two files, one is the private key that's base64-encoded, and has the header, 
"`-----BEGIN RSA PRIVATE KEY-----`", and the other is the public key.
However, that public key isn't in the form we want, so to get the public key in the format we want,
you can use ssh-keygen again, on either Windows or Linux, which dumps the output to the terminal.
Make sure to copy the output to a text file for later.

**`ssh-keygen -f mynewrsakey.pub -e -m pem`**

or (Linux only) you can use openssl to write it to a file, like this:

**`openssl rsa -in mynewrsakey -RSAPublicKey_out`**

### Store the keys in AWS Secrets Manager
I coded this PoC app to pull the keys from Secrets Manager, with both public and private keys stored in a
single "secret". Secrets Manager stores each "row" of a secret as a separate JSON node. You can use the AWS Secrets
Manager console to create a new secret, set the type as "Other type of secrets", and add an additional row
in the "Specify the key/value pairs" section. Give one the name `privateKey` and the other `publicKey` and
paste the appropriate values ** *without the header and footer rows* ** into the values fields. See the screenshot below:

> *Note: I deleted all the carriage-returns from the keys in a text editor first, but I haven't verified that this is necessary*.

![Secrets-Manager-screenshot](docs/secrets-manager.png)

### Update Environment Variables

The Lambda function code (in Function.cs) reads three Environment variables, which are used as string values in the generated JWT.  These values are:

**1. Audience**
TBD

**2. Issuer**
This is the base URL of wherever your "well-known" metadata is accessible. In this case, we're just using an S3 bucket (either already existing, or you can create a new one)
 
**3. KeyId**
An identifier for the key-pair used to encrypt the JWT. This can be any string you like, and it needs to match the "kid" value in the jwks.json file (see below). Currently, the project is set up to use *jwtvendsvckey01* as the KeyId (you can keep that value if you like for this sample use case).

### Deploy and test the Lambda function

**TBD**

![screenshot of successful test in Lambda console](docs/console-test-succeeded.jpg)

### Host the JWKS.json and openid-configuration files in Amazon S3

**TBD**

### Create API Gateway HTTP API with JWT Authorizer

**TBD**

### Test it all out!

**TBD**
