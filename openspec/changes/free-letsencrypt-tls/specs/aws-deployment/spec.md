## MODIFIED Requirements

### Requirement: Traffic to the APIs is served over TLS

Because authentication is HTTP Basic (base64, not encrypted), all client traffic to both APIs SHALL be served over HTTPS, terminated at the node boundary by a reverse proxy on the instance with automatically issued and renewed certificates (e.g. Caddy with Let's Encrypt), or a self-signed internal certificate when no public domain is configured. When no Route 53 zone is available, a free wildcard DNS hostname (e.g. an `sslip.io` or `nip.io` address derived from the instance's Elastic IP) MAY be used as the public domain so the proxy obtains automatically issued and renewed certificates from a public certificate authority (Let's Encrypt) without any domain purchase. Plain HTTP SHALL redirect to HTTPS. The APIs themselves SHALL listen on plain HTTP on their private ports behind the proxy.

#### Scenario: HTTPS terminates at the node boundary

- **WHEN** a client requests `https://<api-host>/health`
- **THEN** the request succeeds through the proxy, which terminates TLS and forwards plain HTTP to the API

#### Scenario: Plain HTTP redirects

- **WHEN** a client requests `http://<api-host>/health`
- **THEN** the response redirects (301/302) to the HTTPS URL

#### Scenario: TLS is enforced without a public domain

- **WHEN** no public domain is configured and the client reaches the APIs over the instance's public IP
- **THEN** the proxy still serves HTTPS (self-signed internal certificate) and plain HTTP is rejected or redirected, so Basic Auth credentials never travel unencrypted

#### Scenario: Free wildcard DNS hostname receives trusted certificates

- **WHEN** a free wildcard DNS hostname derived from the instance's public IP (e.g. `cms.13-39-187-128.sslip.io`) is configured as the public domain
- **THEN** the proxy obtains and renews certificates from a public certificate authority for that hostname, and clients can verify the TLS connection without bypassing certificate validation

#### Scenario: APIs bind all interfaces

- **WHEN** the APIs start on the node
- **THEN** each binds `http://0.0.0.0:<port>` (not the default loopback-only bind), so the proxy can reach them
