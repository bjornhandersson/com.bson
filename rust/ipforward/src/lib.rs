//! # IP Forward Library
//!
//! A bi-directional UDP packet forwarding utility library.
//! This library provides the core functionality for efficiently forwarding UDP data
//! between different network endpoints with bi-directional communication support.

pub mod cli;
pub mod error;
pub mod forwarder;

pub use error::Result;
