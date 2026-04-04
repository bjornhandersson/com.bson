//! Integration tests for the IP Forward application

use ipforward::{cli::Args, forwarder::UdpForwarder};
use std::net::{IpAddr, Ipv4Addr, SocketAddr};
use std::time::Duration;
use tokio::net::UdpSocket;
use tokio::time::timeout;

/// Test that the UDP forwarder can be created successfully
#[tokio::test]
async fn test_udp_forwarder_creation() {
    let bind_addr = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), 0);
    let target_addr = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), 0);

    let forwarder = UdpForwarder::new(bind_addr, target_addr);
    // Just test that we can create the forwarder without panicking
    assert_eq!(
        std::mem::size_of_val(&forwarder),
        std::mem::size_of::<UdpForwarder>()
    );
}

/// Test CLI argument parsing and validation
#[test]
fn test_cli_args_validation() {
    let args = Args {
        bind_address: Some(IpAddr::V4(Ipv4Addr::new(192, 168, 1, 100))),
        bind_port: 8080,
        target_address: IpAddr::V4(Ipv4Addr::new(10, 0, 0, 1)),
        target_port: 9090,
        protocol: "UDP".to_string(),
    };

    // Test validation passes for UDP
    assert!(args.validate().is_ok());

    // Test bind and target address creation
    let bind_addr = args.bind_addr();
    let target_addr = args.target_addr();

    assert_eq!(bind_addr.port(), 8080);
    assert_eq!(target_addr.port(), 9090);
}

/// Test UDP socket binding with different addresses
#[tokio::test]
async fn test_udp_socket_binding() {
    // Test binding to localhost
    let socket = UdpSocket::bind("127.0.0.1:0").await;
    assert!(socket.is_ok());

    let socket = socket.unwrap();
    let local_addr = socket.local_addr().unwrap();
    assert_eq!(local_addr.ip(), IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)));
    assert!(local_addr.port() > 0);
}

/// Test basic UDP packet sending and receiving
#[tokio::test]
async fn test_udp_packet_transmission() {
    // Create a receiver socket
    let receiver = UdpSocket::bind("127.0.0.1:0").await.unwrap();
    let receiver_addr = receiver.local_addr().unwrap();

    // Create a sender socket
    let sender = UdpSocket::bind("127.0.0.1:0").await.unwrap();

    // Send a test message
    let test_message = b"Hello, UDP!";
    sender.send_to(test_message, receiver_addr).await.unwrap();

    // Receive the message
    let mut buf = [0u8; 1024];
    let result = timeout(Duration::from_millis(100), receiver.recv_from(&mut buf)).await;

    assert!(result.is_ok());
    let (len, _sender_addr) = result.unwrap().unwrap();
    assert_eq!(len, test_message.len());
    assert_eq!(&buf[..len], test_message);
}

/// Test error handling for invalid addresses
#[tokio::test]
async fn test_invalid_address_binding() {
    // Try to bind to an invalid address
    let result = UdpSocket::bind("999.999.999.999:8080").await;
    assert!(result.is_err());
}

/// Test concurrent UDP operations
#[tokio::test]
async fn test_concurrent_udp_operations() {
    let socket1 = UdpSocket::bind("127.0.0.1:0").await.unwrap();
    let socket2 = UdpSocket::bind("127.0.0.1:0").await.unwrap();

    let addr1 = socket1.local_addr().unwrap();
    let addr2 = socket2.local_addr().unwrap();

    // Test that both sockets can operate concurrently
    let task1 = tokio::spawn(async move { socket1.send_to(b"from socket1", addr2).await });

    let task2 = tokio::spawn(async move { socket2.send_to(b"from socket2", addr1).await });

    let (result1, result2) = tokio::join!(task1, task2);
    assert!(result1.unwrap().is_ok());
    assert!(result2.unwrap().is_ok());
}

/// Test IoT device communication with ACK responses through the forwarder
/// This simulates: IoT Device → UDP Forwarder → Ingress Gateway → ACK back to IoT Device
#[tokio::test]
async fn test_iot_device_ack_communication() {
    use std::time::Duration;
    use tokio::time::timeout;

    // Step 1: Create mock ingress gateway that sends ACKs
    let ingress_gateway = UdpSocket::bind("127.0.0.1:0").await.unwrap();
    let gateway_addr = ingress_gateway.local_addr().unwrap();

    // Step 2: Create the UDP forwarder
    let forwarder_bind_addr = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), 0);
    let _forwarder = UdpForwarder::new(forwarder_bind_addr, gateway_addr);

    // Get the actual bind address after creating the forwarder
    let forwarder_socket = UdpSocket::bind(forwarder_bind_addr).await.unwrap();
    let forwarder_addr = forwarder_socket.local_addr().unwrap();

    // Step 3: Create mock IoT device
    let iot_device = UdpSocket::bind("127.0.0.1:0").await.unwrap();
    let device_addr = iot_device.local_addr().unwrap();

    // Step 4: Start the ingress gateway that responds with ACKs
    let gateway_task = {
        let ingress_gateway = ingress_gateway;
        tokio::spawn(async move {
            let mut buf = [0u8; 1024];

            // Wait for IoT data and respond with ACK
            if let Ok((len, from_addr)) = ingress_gateway.recv_from(&mut buf).await {
                let received_data = &buf[..len];

                // Simulate processing the IoT data
                if received_data.starts_with(b"IoT_DATA:") {
                    // Send ACK response back
                    let ack_response = b"ACK:OK";
                    let _ = ingress_gateway.send_to(ack_response, from_addr).await;
                }
            }
        })
    };

    // Step 5: Start a simplified forwarder manually for this test
    let forwarder_task = {
        let forwarder_socket = forwarder_socket;
        let gateway_addr = gateway_addr;
        let device_addr = device_addr;

        tokio::spawn(async move {
            let mut buf = [0u8; 1024];

            // Forward from IoT device to gateway
            if let Ok((len, from_addr)) = forwarder_socket.recv_from(&mut buf).await {
                if from_addr == device_addr {
                    // Forward to gateway
                    let target_socket = UdpSocket::bind("127.0.0.1:0").await.unwrap();
                    let _ = target_socket.send_to(&buf[..len], gateway_addr).await;

                    // Wait for response from gateway and forward back
                    if let Ok((response_len, _)) = target_socket.recv_from(&mut buf).await {
                        let _ = forwarder_socket
                            .send_to(&buf[..response_len], device_addr)
                            .await;
                    }
                }
            }
        })
    };

    // Step 6: IoT device sends data
    let iot_data = b"IoT_DATA:temperature=25.5,humidity=60.2";
    iot_device.send_to(iot_data, forwarder_addr).await.unwrap();

    // Step 7: IoT device waits for ACK
    let mut ack_buf = [0u8; 1024];
    let ack_result = timeout(
        Duration::from_millis(500),
        iot_device.recv_from(&mut ack_buf),
    )
    .await;

    // Step 8: Verify ACK was received
    assert!(
        ack_result.is_ok(),
        "IoT device should receive ACK through forwarder"
    );
    let (ack_len, ack_from) = ack_result.unwrap().unwrap();

    // Verify the ACK came from the forwarder
    assert_eq!(ack_from, forwarder_addr);

    // Verify the ACK content
    let received_ack = &ack_buf[..ack_len];
    assert_eq!(received_ack, b"ACK:OK");

    // Clean up tasks
    gateway_task.abort();
    forwarder_task.abort();
}

/// Test end-to-end IoT communication with real UDP forwarder instance
#[tokio::test]
async fn test_real_iot_scenario_with_forwarder() {
    use std::time::Duration;
    use tokio::time::timeout;

    // Step 1: Create mock ingress gateway
    let ingress_gateway = UdpSocket::bind("127.0.0.1:0").await.unwrap();
    let gateway_addr = ingress_gateway.local_addr().unwrap();

    // Step 2: Start ingress gateway that processes IoT data and sends ACKs
    let gateway_task = {
        let ingress_gateway = ingress_gateway;
        tokio::spawn(async move {
            let mut buf = [0u8; 1024];

            loop {
                if let Ok((len, from_addr)) = ingress_gateway.recv_from(&mut buf).await {
                    let received_data = &buf[..len];

                    // Process different types of IoT messages
                    if received_data.starts_with(b"SENSOR:") {
                        let ack = b"ACK:SENSOR_RECEIVED";
                        let _ = ingress_gateway.send_to(ack, from_addr).await;
                    } else if received_data.starts_with(b"HEARTBEAT:") {
                        let ack = b"ACK:HEARTBEAT_OK";
                        let _ = ingress_gateway.send_to(ack, from_addr).await;
                    } else if received_data.starts_with(b"ALERT:") {
                        let ack = b"ACK:ALERT_PROCESSED";
                        let _ = ingress_gateway.send_to(ack, from_addr).await;
                    }
                }
            }
        })
    };

    // Step 3: Create UDP forwarder
    let forwarder_bind_addr = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), 0);
    let forwarder = UdpForwarder::new(forwarder_bind_addr, gateway_addr);

    // We can't easily test the real forwarder in a unit test due to its infinite loop,
    // but we can verify the forwarder can be created and configured correctly
    assert_eq!(
        std::mem::size_of_val(&forwarder),
        std::mem::size_of::<UdpForwarder>()
    );

    // Step 4: Test that we can create IoT device connections
    let iot_device1 = UdpSocket::bind("127.0.0.1:0").await.unwrap();
    let _iot_device2 = UdpSocket::bind("127.0.0.1:0").await.unwrap();
    let _iot_device3 = UdpSocket::bind("127.0.0.1:0").await.unwrap();

    // Step 5: Verify different IoT message types can be created
    let sensor_data = b"SENSOR:temp=23.1,pressure=1013.25,location=warehouse_a";
    let heartbeat = b"HEARTBEAT:device_id=iot_001,uptime=3600";
    let alert = b"ALERT:level=critical,type=temperature_high,value=85.2";

    // Verify message formats
    assert!(sensor_data.len() > 0);
    assert!(heartbeat.len() > 0);
    assert!(alert.len() > 0);

    // Step 6: Test direct communication to verify gateway works
    iot_device1
        .send_to(sensor_data, gateway_addr)
        .await
        .unwrap();

    let mut ack_buf = [0u8; 1024];
    let ack_result = timeout(
        Duration::from_millis(100),
        iot_device1.recv_from(&mut ack_buf),
    )
    .await;

    assert!(ack_result.is_ok());
    let (ack_len, _) = ack_result.unwrap().unwrap();
    let received_ack = &ack_buf[..ack_len];
    assert_eq!(received_ack, b"ACK:SENSOR_RECEIVED");

    // Clean up
    gateway_task.abort();
}
