<?php
/**
 * API Router / Entry Point
 * 
 * All API requests route through this file.
 * Apache rewrite rules direct everything to index.php
 * 
 * Base URL: http://localhost/lms-api/
 */

// ── CORS Headers (allow WPF desktop app to connect) ──
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS');
header('Access-Control-Allow-Headers: Content-Type, Authorization');

// Handle preflight OPTIONS request
if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    http_response_code(204);
    exit;
}

// ── Load Core ──
require_once __DIR__ . '/core/Database.php';
require_once __DIR__ . '/core/Response.php';

// ── Parse the Request ──
$requestUri = $_SERVER['REQUEST_URI'];
$basePath = '/lms-api';

// Strip base path and query string
$path = parse_url($requestUri, PHP_URL_PATH);
$path = str_replace($basePath, '', $path);
$path = trim($path, '/');
$method = $_SERVER['REQUEST_METHOD'];

// Get JSON body for POST/PUT requests
$input = json_decode(file_get_contents('php://input'), true) ?? [];

// ── Route Definitions ──
try {
    // Health check
    if ($path === '' || $path === 'health') {
        Response::success([
            'status'  => 'online',
            'version' => '1.0.0',
            'system'  => 'Lead Management System API',
            'time'    => date('Y-m-d H:i:s'),
        ], 'API is running');
    }

    // Auth Routes
    elseif ($path === 'auth/login' && $method === 'POST') {
        require_once __DIR__ . '/controllers/AuthController.php';
        $controller = new AuthController();
        $controller->login($input);
    }

    elseif ($path === 'auth/me' && $method === 'GET') {
        require_once __DIR__ . '/controllers/AuthController.php';
        $controller = new AuthController();
        $controller->me();
    }

    // User Routes
    elseif ($path === 'users' && $method === 'GET') {
        require_once __DIR__ . '/controllers/UserController.php';
        $controller = new UserController();
        $controller->index();
    }

    elseif ($path === 'users' && $method === 'POST') {
        require_once __DIR__ . '/controllers/UserController.php';
        $controller = new UserController();
        $controller->store($input);
    }

    // Lead Routes
    elseif ($path === 'leads/import' && $method === 'POST') {
        require_once __DIR__ . '/controllers/LeadController.php';
        $controller = new LeadController();
        $controller->import($input);
    }

    elseif ($path === 'leads/stats' && $method === 'GET') {
        require_once __DIR__ . '/controllers/LeadController.php';
        $controller = new LeadController();
        $controller->stats();
    }

    elseif ($path === 'leads/filters' && $method === 'GET') {
        require_once __DIR__ . '/controllers/LeadController.php';
        $controller = new LeadController();
        $controller->filters();
    }

    elseif ($path === 'leads' && $method === 'GET') {
        require_once __DIR__ . '/controllers/LeadController.php';
        $controller = new LeadController();
        $controller->index();
    }

    // Batch Audit History Routes
    elseif ($path === 'batches' && $method === 'GET') {
        require_once __DIR__ . '/controllers/LeadController.php';
        $controller = new LeadController();
        $controller->batches();
    }

    // 404 — No matching route
    else {
        Response::notFound("Endpoint not found: {$method} /{$path}");
    }

} catch (PDOException $e) {
    Response::serverError('Database error: ' . $e->getMessage());
} catch (Exception $e) {
    Response::serverError($e->getMessage());
}
