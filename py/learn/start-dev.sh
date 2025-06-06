#!/bin/bash

# Start development servers for both backend and frontend

echo "Starting Python Web App Development Servers..."
echo "=============================================="

# Function to cleanup background processes
cleanup() {
    echo ""
    echo "Shutting down servers..."
    kill $BACKEND_PID $FRONTEND_PID 2>/dev/null
    exit 0
}

# Set up signal handlers
trap cleanup SIGINT SIGTERM

# Start Flask backend
echo "Starting Flask backend on http://localhost:8000..."
python3 -m src.python_web_app.main &
BACKEND_PID=$!

# Wait a moment for backend to start
sleep 2

# Start React frontend
echo "Starting React frontend on http://localhost:3000..."
cd frontend
npm start &
FRONTEND_PID=$!
cd ..

echo ""
echo "✅ Both servers are starting up!"
echo "📱 Frontend: http://localhost:3000"
echo "🔧 Backend API: http://localhost:8000"
echo ""
echo "Press Ctrl+C to stop both servers"

# Wait for both processes
wait $BACKEND_PID $FRONTEND_PID