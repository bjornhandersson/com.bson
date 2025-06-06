# Security Notes

## Development Dependencies

This project uses Create React App (react-scripts) which includes webpack-dev-server for development. There are currently known moderate severity vulnerabilities in webpack-dev-server that could potentially allow source code theft when accessing malicious websites during development.

### Mitigation

1. **Production builds are not affected** - These vulnerabilities only affect the development server
2. **Use trusted networks** - Only run the development server on trusted networks
3. **Avoid malicious sites** - Don't browse to untrusted websites while the development server is running
4. **Use production builds** - For any public deployment, use `npm run build` to create production builds

### Current Status

- **3 moderate vulnerabilities** in development dependencies only
- **0 vulnerabilities** in production runtime code
- All vulnerabilities are in webpack-dev-server (development only)

### Monitoring

We monitor security advisories and will update dependencies when safe fixes become available that don't break the build system.
