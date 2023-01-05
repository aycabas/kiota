---
parent: Get started
---

# Build SDKs for Ruby

## Required tools

- [Ruby 3](https://www.ruby-lang.org/en/downloads/)

## Initializing target project

Before you can compile and run the target project, you will need to initialize it. After initializing the test project, you will need to add references to the [abstraction](https://github.com/microsoft/kiota-abstractions-ruby), [authentication](https://github.com/microsoft/kiota-authentication-oauth-ruby), [http](https://github.com/microsoft/kiota-http-ruby), and [serialization JSON](https://github.com/microsoft/kiota-serialization-json-ruby) packages.

### Install Bundler

1. To install bundler, execute the following line in the root directory of your project:

    ````shell
    gem install bundler
    ````

2. Create a gemfile named **Gemfile** in the root directory of your project.

### Installing the necessary packages

1. In your project, update your `Gemfile` with the required information. Add these lines to your application's Gemfile:

    ````ruby
    source 'https://rubygems.org'

    gem "microsoft_kiota_abstractions"
    gem "microsoft_kiota_serialization_json"
    gem "microsoft_kiota_authentication_oauth"
    gem "microsoft_kiota_faraday"
    ````

    Only the first package, `microsoft_kiota_abstractions`, is required. The other packages provide default implementations that you can choose to replace with your own implementations if you wish.

2. Finally, install your gems. Execute this line:

    ````shell
    bundle install
    ````

### Generating the client

Kiota generates SDKs from OpenAPI documents. Create a file named **getme.yml** and add the contents of the [Sample OpenAPI description](https://github.com/microsoft/kiota/blob/main/docs/get-started/reference-openapi.md).

You can then use the Kiota command line tool to generate the SDK classes.

````shell
kiota generate -l ruby -d getme.yml -n Graph -o ./client
````

## Registering an application in Azure AD

## Creating an application registration

> **Note:** this step is required if your client will be calling APIs that are protected by the Microsoft Identity Platform like Microsoft Graph.

Follow the instructions in [Register an application for Microsoft identity platform authentication](register-app.md) to get an application ID (also know as a client ID).

## Creating the client application

1. Create a file in the root of the project named **get_user.rb** and add the following code.

    Replace the `tenant_id`, `client_id`, `client_secret` with your credentials from the previous step.

    See [Get access on behalf of a user](https://learn.microsoft.com/graph/auth-v2-user?context=graph%2Fapi%2F1.0&view=graph-rest-1.0) for details on how to get the `auth_code` and set the `redirect_uri`.

    > **Note:** If you need help generating the authorize url, the line
    >
    > ````ruby
    > puts token_request_context.generate_authorize_url(graph_scopes)
    >  ````
    >
    >  (after you've initialized your `token_request_context` of course) will print out a corresponding authorization url, with which you can retrieve your authorization code. You can also re-set the `auth_code` after you've initialized the `token_request_context` like so:
    >
    >  ````ruby
    >  token_request_context.auth_code = 'CODE'
    >  ````

    ````ruby
    # frozen_string_literal: true
    require 'microsoft_kiota_serialization_json'
    require 'microsoft_kiota_abstractions'
    require 'microsoft_kiota_authentication_oauth'
    require 'microsoft_kiota_faraday'
    require_relative './client/api_client'

    tenant_id = 'TENANT_ID'
    client_id = 'CLIENT_ID'
    client_secret = 'CLIENT_SECRET'
    auth_code = 'AUTH_CODE'
    redirect_uri = 'REDIRECT_URI'

    # The auth provider will only authorize requests to
    # the allowed hosts, in this case Microsoft Graph
    allowed_hosts = ['graph.microsoft.com']
    graph_scopes = ['User.Read']
    token_request_context = MicrosoftKiotaAuthenticationOAuth::AuthorizationCodeContext.new(tenant_id, client_id, client_secret, redirect_uri, auth_code)

    auth_provider = MicrosoftKiotaAuthenticationOAuth::OAuthAuthenticationProvider.new(token_request_context, allowed_hosts, graph_scopes)

    request_adapter = MicrosoftKiotaNethttplibrary::NetHttpRequestAdapter.new(auth_provider)

    client = Graph::ApiClient.new(request_adapter)
    me = client.me.get.resume
    puts "Hi! My name is #{me.display_name}, and my ID is #{me.id}."

    ````

2. Lastly, create a file called **graph.rb** in the `client` folder that was just created by Kiota. Please add the following code:

    ````ruby
    # frozen_string_literal: true
    module Graph
    end
    ````

## Executing the application

When ready to execute the application, execute the following command in your project directory.

````shell
ruby ./get_user.rb
````

## See also

- [kiota-samples repository](https://github.com/microsoft/kiota-samples/tree/main/get-started/ruby) contains the code from this guide.
