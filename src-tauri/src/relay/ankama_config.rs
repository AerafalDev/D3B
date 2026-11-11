use std::net::{IpAddr, Ipv4Addr};

use serde_json::{Map, Value};

use super::RelayError;

/// Ankama's configuration, the JSON the client's `ConfigUrl` names: 768 bytes and 15 keys on 2026-10-27 (inferred,
/// `main:docs/client.md`, "Configuration", 3.7.12.12). The client takes its connection host from `connectionHosts`;
/// every other key (the chat, Haapi, the shop) keeps its value, though not its order or formatting.
#[derive(Debug, Clone, PartialEq)]
pub(crate) struct AnkamaConfig {
    json: Map<String, Value>,
    host: ConnectionHost,
}

/// The one entry of `connectionHosts`, `<name>:<host>:<ports>`, ports `5555,443` (inferred, `main:docs/client.md`,
/// "Configuration", 3.7.12.12).
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct ConnectionHost {
    pub(crate) name: String,
    pub(crate) host: String,
    pub(crate) ports: Vec<u16>,
}

impl AnkamaConfig {
    /// The most the configuration may hold: about a hundred times what Ankama serves.
    pub(crate) const LIMIT: usize = 64 * 1024;

    const HOSTS: &'static str = "connectionHosts";

    /// Reads the configuration, refusing one D3B cannot relay: more than one connection host (a relayed connection
    /// carries no host name to tell them apart) or a loopback host (a configuration already redirected).
    pub(crate) fn parse(text: &str) -> Result<Self, RelayError> {
        if text.len() > Self::LIMIT {
            return Err(RelayError::ConfigTooLarge {
                size: text.len(),
                limit: Self::LIMIT,
            });
        }
        let json: Map<String, Value> =
            serde_json::from_str(text).map_err(|error| RelayError::ConfigNotJson(error.to_string()))?;
        let entries = json
            .get(Self::HOSTS)
            .and_then(Value::as_array)
            .ok_or(RelayError::NoConnectionHosts)?;
        let [entry] = entries.as_slice() else {
            return Err(RelayError::ConnectionHosts(entries.len()));
        };
        let host = ConnectionHost::parse(entry.as_str().ok_or(RelayError::NoConnectionHosts)?)?;
        if host.is_loopback() {
            return Err(RelayError::AlreadyRedirected(host.host));
        }
        Ok(Self { json, host })
    }

    /// Ankama's connection host, the one the relay connects to.
    pub(crate) fn connection_host(&self) -> &ConnectionHost {
        &self.host
    }

    /// The configuration with its connection host replaced by `address`, the ports and every other key kept: the
    /// copy the client reads through its `file:` URL.
    pub(crate) fn redirected_to(&self, address: Ipv4Addr) -> String {
        let mut json = self.json.clone();
        let host = ConnectionHost {
            host: address.to_string(),
            ..self.host.clone()
        };
        json.insert(Self::HOSTS.to_owned(), Value::from(vec![host.entry()]));
        Value::Object(json).to_string()
    }
}

impl ConnectionHost {
    /// Reads an entry `<name>:<host>:<port>,<port>`.
    pub(crate) fn parse(entry: &str) -> Result<Self, RelayError> {
        let malformed = || RelayError::ConnectionHostMalformed(entry.to_owned());
        let mut fields = entry.split(':');
        let (Some(name), Some(host), Some(list), None) = (fields.next(), fields.next(), fields.next(), fields.next())
        else {
            return Err(malformed());
        };
        if name.is_empty() || !is_host_name(host) {
            return Err(malformed());
        }
        let ports = list
            .split(',')
            .map(|port| port.parse::<u16>().ok().filter(|&port| port != 0))
            .collect::<Option<Vec<_>>>()
            .ok_or_else(malformed)?;
        Ok(Self {
            name: name.to_owned(),
            host: host.to_owned(),
            ports,
        })
    }

    /// The entry as the configuration lists it.
    pub(crate) fn entry(&self) -> String {
        let ports: Vec<String> = self.ports.iter().map(u16::to_string).collect();
        format!("{}:{}:{}", self.name, self.host, ports.join(","))
    }

    fn is_loopback(&self) -> bool {
        self.host.eq_ignore_ascii_case("localhost") || self.host.parse::<IpAddr>().is_ok_and(|ip| ip.is_loopback())
    }
}

/// Whether `host` is a DNS name or an IPv4 address: dot-separated labels of letters, digits and hyphens. A host of
/// digits and dots only must be a full IPv4 address: a short form such as `127.1` would reach loopback unseen.
pub(crate) fn is_host_name(host: &str) -> bool {
    if host.bytes().all(|byte| byte.is_ascii_digit() || byte == b'.') {
        return host.parse::<Ipv4Addr>().is_ok();
    }
    host.len() <= 253
        && host.split('.').all(|label| {
            (1..=63).contains(&label.len())
                && !label.starts_with('-')
                && !label.ends_with('-')
                && label.bytes().all(|byte| byte.is_ascii_alphanumeric() || byte == b'-')
        })
}

#[cfg(test)]
mod tests {
    use super::*;

    const CONFIG: &str = r#"{"chatHost":"chat.example.com:6337","connectionHosts":["production:connection.example.com:5555,443"],"haapiUrl":"https://haapi.example.com"}"#;

    #[test]
    fn reads_the_connection_host() {
        let config = AnkamaConfig::parse(CONFIG).unwrap_or_else(|error| panic!("{error}"));

        assert_eq!(
            config.connection_host(),
            &ConnectionHost {
                name: "production".into(),
                host: "connection.example.com".into(),
                ports: vec![5555, 443],
            }
        );
    }

    #[test]
    fn redirects_the_host_and_keeps_the_ports_and_other_keys() {
        let config = AnkamaConfig::parse(CONFIG).unwrap_or_else(|error| panic!("{error}"));

        let copy: Value = serde_json::from_str(&config.redirected_to(crate::relay::ADDRESS))
            .unwrap_or_else(|error| panic!("{error}"));

        let expected: Value = serde_json::from_str(
            r#"{"chatHost":"chat.example.com:6337","connectionHosts":["production:127.0.13.59:5555,443"],"haapiUrl":"https://haapi.example.com"}"#,
        )
        .unwrap_or_else(|error| panic!("{error}"));
        assert_eq!(copy, expected);
    }

    #[test]
    fn refuses_two_connection_hosts_or_none() {
        let two = r#"{"connectionHosts":["a:one.example.com:5555","b:two.example.com:5555"]}"#;
        assert!(matches!(AnkamaConfig::parse(two), Err(RelayError::ConnectionHosts(2))));

        let none = r#"{"connectionHosts":[]}"#;
        assert!(matches!(AnkamaConfig::parse(none), Err(RelayError::ConnectionHosts(0))));
    }

    #[test]
    fn refuses_a_configuration_without_connection_hosts() {
        for text in [
            r#"{"chatHost":"x"}"#,
            r#"{"connectionHosts":"a:b:1"}"#,
            r#"{"connectionHosts":[5]}"#,
        ] {
            assert!(
                matches!(AnkamaConfig::parse(text), Err(RelayError::NoConnectionHosts)),
                "{text}"
            );
        }
    }

    #[test]
    fn refuses_what_is_not_a_json_object() {
        for text in ["", "[1]", "{"] {
            assert!(
                matches!(AnkamaConfig::parse(text), Err(RelayError::ConfigNotJson(_))),
                "{text}"
            );
        }
    }

    #[test]
    fn refuses_a_configuration_already_redirected() {
        for host in ["127.0.13.59", "127.0.0.1", "localhost", "LocalHost"] {
            let text = format!(r#"{{"connectionHosts":["p:{host}:5555"]}}"#);
            assert!(
                matches!(AnkamaConfig::parse(&text), Err(RelayError::AlreadyRedirected(_))),
                "{host}"
            );
        }
    }

    #[test]
    fn refuses_its_own_copy() {
        let config = AnkamaConfig::parse(CONFIG).unwrap_or_else(|error| panic!("{error}"));

        let copy = AnkamaConfig::parse(&config.redirected_to(crate::relay::ADDRESS));

        assert!(matches!(copy, Err(RelayError::AlreadyRedirected(_))), "{copy:?}");
    }

    #[test]
    fn accepts_a_configuration_of_exactly_the_limit() {
        let mut text = CONFIG.to_owned();
        text.push_str(&" ".repeat(AnkamaConfig::LIMIT - CONFIG.len()));

        assert!(AnkamaConfig::parse(&text).is_ok());
    }

    #[test]
    fn refuses_a_configuration_over_the_limit() {
        let text = " ".repeat(AnkamaConfig::LIMIT + 1);

        assert!(matches!(
            AnkamaConfig::parse(&text),
            Err(RelayError::ConfigTooLarge { size: 65_537, .. })
        ));
    }

    #[test]
    fn refuses_a_malformed_entry() {
        for entry in [
            "p:host.example.com",
            ":host.example.com:5555",
            "p::5555",
            "p:host.example.com:",
            "p:host.example.com:0",
            "p:host.example.com:65536",
            "p:host.example.com:5555,x",
            "p:-host.example.com:5555",
            "p:host..example.com:5555",
            "p:host_1.example.com:5555",
            "p:host.example.com:5555:1",
            "p:::1:5555",
            "p:127.1:5555",
            "p:2130706433:5555",
            "p:1.2.3.4.5:5555",
        ] {
            assert!(
                matches!(
                    ConnectionHost::parse(entry),
                    Err(RelayError::ConnectionHostMalformed(_))
                ),
                "{entry}"
            );
        }
    }

    #[test]
    fn accepts_an_ipv4_host() {
        let host = ConnectionHost::parse("p:203.0.113.7:5555").unwrap_or_else(|error| panic!("{error}"));

        assert_eq!(host.host, "203.0.113.7");
    }

    #[test]
    fn an_entry_reads_back() {
        let entry = "production:connection.example.com:5555,443";

        assert_eq!(
            ConnectionHost::parse(entry)
                .unwrap_or_else(|error| panic!("{error}"))
                .entry(),
            entry
        );
    }
}
