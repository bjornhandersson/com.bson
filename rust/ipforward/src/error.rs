//! Error handling for the IP Forward application

use std::fmt;

/// Result type alias for the application
pub type Result<T> = std::result::Result<T, Error>;

/// Application error types
#[derive(Debug)]
pub enum Error {
    /// IO errors (network, file system, etc.)
    Io(std::io::Error),
    /// Configuration errors
    Config(String),
    /// Network errors
    Network(String),
    /// Generic application errors
    App(String),
}

impl fmt::Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Error::Io(err) => write!(f, "IO error: {}", err),
            Error::Config(msg) => write!(f, "Configuration error: {}", msg),
            Error::Network(msg) => write!(f, "Network error: {}", msg),
            Error::App(msg) => write!(f, "Application error: {}", msg),
        }
    }
}

impl std::error::Error for Error {
    fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
        match self {
            Error::Io(err) => Some(err),
            _ => None,
        }
    }
}

impl From<std::io::Error> for Error {
    fn from(err: std::io::Error) -> Self {
        Error::Io(err)
    }
}

impl From<anyhow::Error> for Error {
    fn from(err: anyhow::Error) -> Self {
        Error::App(err.to_string())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::error::Error as StdError;
    use std::io;

    #[test]
    fn test_error_display() {
        let io_err = Error::Io(io::Error::new(io::ErrorKind::NotFound, "file not found"));
        assert_eq!(format!("{}", io_err), "IO error: file not found");

        let config_err = Error::Config("invalid configuration".to_string());
        assert_eq!(
            format!("{}", config_err),
            "Configuration error: invalid configuration"
        );

        let network_err = Error::Network("connection failed".to_string());
        assert_eq!(
            format!("{}", network_err),
            "Network error: connection failed"
        );

        let app_err = Error::App("application error".to_string());
        assert_eq!(
            format!("{}", app_err),
            "Application error: application error"
        );
    }

    #[test]
    fn test_error_from_io_error() {
        let io_err = io::Error::new(io::ErrorKind::PermissionDenied, "access denied");
        let app_err: Error = io_err.into();

        match app_err {
            Error::Io(err) => assert_eq!(err.kind(), io::ErrorKind::PermissionDenied),
            _ => panic!("Expected IO error"),
        }
    }

    #[test]
    fn test_error_from_anyhow_error() {
        let anyhow_err = anyhow::anyhow!("test error");
        let app_err: Error = anyhow_err.into();

        match app_err {
            Error::App(msg) => assert_eq!(msg, "test error"),
            _ => panic!("Expected App error"),
        }
    }

    #[test]
    fn test_error_source() {
        let io_err = io::Error::new(io::ErrorKind::TimedOut, "timeout");
        let app_err = Error::Io(io_err);

        assert!(StdError::source(&app_err).is_some());

        let config_err = Error::Config("test".to_string());
        assert!(StdError::source(&config_err).is_none());
    }
}
