//! UDP packet forwarding implementation

use crate::Result;
use std::collections::HashMap;
use std::net::SocketAddr;
use std::sync::Arc;
use tokio::net::UdpSocket;
use tokio::sync::Mutex;
use tracing::{error, info, warn};

/// UDP packet forwarder that handles bi-directional communication
pub struct UdpForwarder {
    bind_addr: SocketAddr,
    target_addr: SocketAddr,
}

impl UdpForwarder {
    /// Create a new UDP forwarder
    pub fn new(bind_addr: SocketAddr, target_addr: SocketAddr) -> Self {
        Self {
            bind_addr,
            target_addr,
        }
    }

    /// Start the UDP forwarding service
    pub async fn start(&self) -> Result<()> {
        info!(
            "Starting UDP forwarding from {} to {}",
            self.bind_addr, self.target_addr
        );

        // Bind to the local address
        let local_socket = UdpSocket::bind(self.bind_addr).await?;
        info!("Bound to local address: {}", self.bind_addr);

        // Create a socket for communicating with the target
        let target_socket = UdpSocket::bind("0.0.0.0:0").await?;
        info!("Created target socket on: {}", target_socket.local_addr()?);

        // Store client mappings for bi-directional communication
        let client_map: Arc<Mutex<HashMap<SocketAddr, SocketAddr>>> =
            Arc::new(Mutex::new(HashMap::new()));

        // Clone references for the tasks
        let local_socket = Arc::new(local_socket);
        let target_socket = Arc::new(target_socket);

        // Task 1: Forward packets from clients to target
        let local_to_target = self.spawn_local_to_target_task(
            Arc::clone(&local_socket),
            Arc::clone(&target_socket),
            Arc::clone(&client_map),
        );

        // Task 2: Forward packets from target back to clients
        let target_to_local = self.spawn_target_to_local_task(
            Arc::clone(&local_socket),
            Arc::clone(&target_socket),
            Arc::clone(&client_map),
        );

        println!("UDP forwarding started successfully!");
        println!("Press Ctrl+C to stop...");

        // Wait for both tasks (they run indefinitely)
        tokio::select! {
            result = local_to_target => {
                if let Err(e) = result {
                    error!("Local to target task failed: {}", e);
                }
            }
            result = target_to_local => {
                if let Err(e) = result {
                    error!("Target to local task failed: {}", e);
                }
            }
            _ = tokio::signal::ctrl_c() => {
                info!("Received Ctrl+C, shutting down...");
            }
        }

        Ok(())
    }

    /// Spawn task to forward packets from local clients to target
    fn spawn_local_to_target_task(
        &self,
        local_socket: Arc<UdpSocket>,
        target_socket: Arc<UdpSocket>,
        client_map: Arc<Mutex<HashMap<SocketAddr, SocketAddr>>>,
    ) -> tokio::task::JoinHandle<()> {
        let target_addr = self.target_addr;

        tokio::spawn(async move {
            let mut buf = [0u8; 65536];

            loop {
                match local_socket.recv_from(&mut buf).await {
                    Ok((len, client_addr)) => {
                        info!("Received {} bytes from client {}", len, client_addr);

                        // Store client mapping for return packets
                        {
                            let mut map = client_map.lock().await;
                            map.insert(target_addr, client_addr);
                        }

                        // Forward to target
                        if let Err(e) = target_socket.send_to(&buf[..len], target_addr).await {
                            error!("Failed to forward packet to target {}: {}", target_addr, e);
                        } else {
                            info!("Forwarded {} bytes to target {}", len, target_addr);
                        }
                    }
                    Err(e) => {
                        error!("Error receiving from local socket: {}", e);
                    }
                }
            }
        })
    }

    /// Spawn task to forward packets from target back to local clients
    fn spawn_target_to_local_task(
        &self,
        local_socket: Arc<UdpSocket>,
        target_socket: Arc<UdpSocket>,
        client_map: Arc<Mutex<HashMap<SocketAddr, SocketAddr>>>,
    ) -> tokio::task::JoinHandle<()> {
        tokio::spawn(async move {
            let mut buf = [0u8; 65536];

            loop {
                match target_socket.recv_from(&mut buf).await {
                    Ok((len, from_addr)) => {
                        info!("Received {} bytes from target {}", len, from_addr);

                        // Find the client to send back to
                        let client_addr = {
                            let map = client_map.lock().await;
                            map.get(&from_addr).copied()
                        };

                        if let Some(client_addr) = client_addr {
                            if let Err(e) = local_socket.send_to(&buf[..len], client_addr).await {
                                error!(
                                    "Failed to forward packet back to client {}: {}",
                                    client_addr, e
                                );
                            } else {
                                info!("Forwarded {} bytes back to client {}", len, client_addr);
                            }
                        } else {
                            warn!(
                                "Received packet from unknown target {}, dropping",
                                from_addr
                            );
                        }
                    }
                    Err(e) => {
                        error!("Error receiving from target socket: {}", e);
                    }
                }
            }
        })
    }
}
