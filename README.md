# SSHttp

Simple API to broker SSH sessions through HTTP requests.

## Endpoints

You can find details about each of the endpoints from the swagger doc that should be available at the root of the site e.g. https://sshttp.cray.io/

## SSH Authentication

This API should support password auth, certificate auth (with and without passphrase) and a combination of the two.

## Deploying

I suggest that you use docker compose to deploy this.

```dockerfile
services:
  sshttp:
    image: 'craysiii/sshttp:latest'
    restart: unless-stopped
    ports:
      - '9651:8080'
    environment:
      API_KEY: '<random string>'
```

## API Authentication

You must pass an `API_KEY` header that matches the `API_KEY` environment variable you set server side. If you do not provide one, a random one will be generated each runtime and printed to stdout.

## License

MIT.