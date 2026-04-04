//! # IP Forward
//!
//! A bi-directional UDP packet forwarding utility.
//! This program will efficiently forward UDP data from the machine it's running on
//! to another IP address and handle bi-directional communication.

use ipforward::{cli::Args, forwarder::UdpForwarder, Result};
use tracing::error;

/// Main entry point for the IP Forward application
#[tokio::main]
async fn main() -> Result<()> {
    // Initialize logging
    tracing_subscriber::fmt::init();

    // Parse command-line arguments
    let args = Args::parse();

    // Validate configuration
    args.validate()?;

    // Print configuration
    args.print_config();

    // Create and start the UDP forwarder
    let forwarder = UdpForwarder::new(args.bind_addr(), args.target_addr());

    if let Err(e) = forwarder.start().await {
        error!("UDP forwarding failed: {}", e);
        std::process::exit(1);
    }

    Ok(())
}
