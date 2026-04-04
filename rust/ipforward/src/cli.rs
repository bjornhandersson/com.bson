//! Command-line interface for the IP Forward application

use clap::Parser;
use std::net::{IpAddr, SocketAddr};

/// A bi-directional UDP packet forwarding utility
#[derive(Parser)]
#[command(name = "ipforward")]
#[command(version = env!("CARGO_PKG_VERSION"))]
#[command(about = "A bi-directional UDP packet forwarding utility")]
pub struct Args {
    /// Bind IP address to listen on locally (e.g., 192.32.0.1).
    /// If not specified, will bind to 0.0.0.0 and listen on all interfaces
    #[arg(short = 'b', long = "bind", value_name = "IP")]
    pub bind_address: Option<IpAddr>,

    /// Bind port to listen on locally (e.g., 4000)
    #[arg(short = 'B', long = "bind-port", value_name = "PORT")]
    pub bind_port: u16,

    /// Target IP address to forward packets to (e.g., 243.234.23.1)
    #[arg(short = 't', long = "target", value_name = "IP")]
    pub target_address: IpAddr,

    /// Target port to forward packets to (e.g., 4001)
    #[arg(short = 'T', long = "target-port", value_name = "PORT")]
    pub target_port: u16,

    /// Protocol to use (currently only UDP is supported)
    #[arg(
        short = 'p',
        long = "protocol",
        value_name = "PROTOCOL",
        default_value = "UDP"
    )]
    pub protocol: String,
}

impl Args {
    /// Parse command-line arguments
    pub fn parse() -> Self {
        <Self as Parser>::parse()
    }

    /// Get the bind socket address, using default IP if not specified
    pub fn bind_addr(&self) -> SocketAddr {
        let bind_ip = self
            .bind_address
            .unwrap_or_else(|| "0.0.0.0".parse().unwrap());
        SocketAddr::new(bind_ip, self.bind_port)
    }

    /// Get the target socket address
    pub fn target_addr(&self) -> SocketAddr {
        SocketAddr::new(self.target_address, self.target_port)
    }

    /// Validate the configuration
    pub fn validate(&self) -> crate::Result<()> {
        if self.protocol.to_uppercase() != "UDP" {
            return Err(crate::error::Error::Config(
                "Only UDP protocol is currently supported".to_string(),
            ));
        }
        Ok(())
    }

    /// Print the configuration
    pub fn print_config(&self) {
        println!("IP Forward v{}", env!("CARGO_PKG_VERSION"));
        println!("A bi-directional UDP packet forwarding utility");
        println!();
        println!("Configuration:");
        println!(
            "  Bind address:   {} {}",
            self.bind_addr(),
            if self.bind_address.is_none() {
                "(default - listening on all interfaces)"
            } else {
                ""
            }
        );
        println!("  Target address: {}", self.target_addr());
        println!("  Protocol:       {}", self.protocol);
        println!();
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::net::{IpAddr, Ipv4Addr};

    #[test]
    fn test_bind_addr_with_explicit_ip() {
        let args = Args {
            bind_address: Some(IpAddr::V4(Ipv4Addr::new(192, 168, 1, 100))),
            bind_port: 8080,
            target_address: IpAddr::V4(Ipv4Addr::new(10, 0, 0, 1)),
            target_port: 9090,
            protocol: "UDP".to_string(),
        };

        let bind_addr = args.bind_addr();
        assert_eq!(bind_addr.ip(), IpAddr::V4(Ipv4Addr::new(192, 168, 1, 100)));
        assert_eq!(bind_addr.port(), 8080);
    }

    #[test]
    fn test_bind_addr_with_default_ip() {
        let args = Args {
            bind_address: None,
            bind_port: 8080,
            target_address: IpAddr::V4(Ipv4Addr::new(10, 0, 0, 1)),
            target_port: 9090,
            protocol: "UDP".to_string(),
        };

        let bind_addr = args.bind_addr();
        assert_eq!(bind_addr.ip(), IpAddr::V4(Ipv4Addr::new(0, 0, 0, 0)));
        assert_eq!(bind_addr.port(), 8080);
    }

    #[test]
    fn test_target_addr() {
        let args = Args {
            bind_address: None,
            bind_port: 8080,
            target_address: IpAddr::V4(Ipv4Addr::new(10, 0, 0, 1)),
            target_port: 9090,
            protocol: "UDP".to_string(),
        };

        let target_addr = args.target_addr();
        assert_eq!(target_addr.ip(), IpAddr::V4(Ipv4Addr::new(10, 0, 0, 1)));
        assert_eq!(target_addr.port(), 9090);
    }

    #[test]
    fn test_validate_udp_protocol() {
        let args = Args {
            bind_address: None,
            bind_port: 8080,
            target_address: IpAddr::V4(Ipv4Addr::new(10, 0, 0, 1)),
            target_port: 9090,
            protocol: "UDP".to_string(),
        };

        assert!(args.validate().is_ok());
    }

    #[test]
    fn test_validate_udp_protocol_case_insensitive() {
        let args = Args {
            bind_address: None,
            bind_port: 8080,
            target_address: IpAddr::V4(Ipv4Addr::new(10, 0, 0, 1)),
            target_port: 9090,
            protocol: "udp".to_string(),
        };

        assert!(args.validate().is_ok());
    }

    #[test]
    fn test_validate_invalid_protocol() {
        let args = Args {
            bind_address: None,
            bind_port: 8080,
            target_address: IpAddr::V4(Ipv4Addr::new(10, 0, 0, 1)),
            target_port: 9090,
            protocol: "TCP".to_string(),
        };

        assert!(args.validate().is_err());
    }
}
