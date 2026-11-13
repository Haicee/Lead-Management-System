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

    elseif ($path === 'leads/query' && $method === 'POST') {
        require_once __DIR__ . '/controllers/LeadController.php';
        $controller = new LeadController();
        $controller->query($input);
    }

    elseif (($path === 'leads/update' && $method === 'POST') || (preg_match('#^leads/(\d+)$#', $path, $matches) && ($method === 'POST' || $method === 'PUT'))) {
        require_once __DIR__ . '/controllers/LeadController.php';
        $controller = new LeadController();
        $leadId = isset($matches[1]) ? (int)$matches[1] : (int)($input['id'] ?? 0);
        $controller->update($leadId, $input);
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

    elseif ((preg_match('#^batches/(\d+)/leads$#', $path, $matches) || $path === 'batches/leads') && $method === 'GET') {
        require_once __DIR__ . '/controllers/LeadController.php';
        $controller = new LeadController();
        $batchId = isset($matches[1]) ? (int)$matches[1] : (int)($_GET['batch_id'] ?? 0);
        $controller->batchLeads($batchId);
    }

    elseif (($path === 'batches/delete' && $method === 'POST') || (preg_match('#^batches/(\d+)$#', $path, $matches) && $method === 'DELETE')) {
        require_once __DIR__ . '/controllers/LeadController.php';
        $controller = new LeadController();
        $batchId = isset($matches[1]) ? (int)$matches[1] : (int)($input['id'] ?? $input['batch_id'] ?? 0);
        $controller->deleteBatch($batchId);
    }

    // Client Routes
    elseif ($path === 'clients' && $method === 'GET') {
        require_once __DIR__ . '/controllers/ClientController.php';
        $controller = new ClientController();
        $controller->index();
    }

    elseif ($path === 'clients' && $method === 'POST') {
        require_once __DIR__ . '/controllers/ClientController.php';
        $controller = new ClientController();
        $controller->store($input);
    }

    elseif (preg_match('#^clients/(\d+)/leads$#', $path, $matches) && $method === 'GET') {
        require_once __DIR__ . '/controllers/ClientController.php';
        $controller = new ClientController();
        $controller->clientLeads((int)$matches[1]);
    }

    elseif (($path === 'clients/leads/update' && $method === 'POST') || (preg_match('#^clients/leads/(\d+)$#', $path, $matches) && ($method === 'POST' || $method === 'PUT'))) {
        require_once __DIR__ . '/controllers/ClientController.php';
        $controller = new ClientController();
        $leadId = isset($matches[1]) ? (int)$matches[1] : (int)($input['id'] ?? 0);
        $controller->updateLead($leadId, $input);
    }

    elseif ((preg_match('#^clients/files/(\d+)/leads$#', $path, $matches) || preg_match('#^client-files/(\d+)/leads$#', $path, $matches)) && $method === 'GET') {
        require_once __DIR__ . '/controllers/ClientController.php';
        $controller = new ClientController();
        $controller->fileLeads((int)$matches[1]);
    }

    elseif ((preg_match('#^clients/files/(\d+)$#', $path, $matches) && $method === 'DELETE') || ($path === 'clients/files/delete' && $method === 'POST')) {
        require_once __DIR__ . '/controllers/ClientController.php';
        $controller = new ClientController();
        $fileId = isset($matches[1]) ? (int)$matches[1] : (int)($input['id'] ?? $input['file_id'] ?? 0);
        $controller->deleteFile($fileId);
    }

    elseif ((preg_match('#^clients/files/(\d+)$#', $path, $matches) && ($method === 'PUT' || $method === 'POST')) || ($path === 'clients/files/update' && $method === 'POST')) {
        require_once __DIR__ . '/controllers/ClientController.php';
        $controller = new ClientController();
        $fileId = isset($matches[1]) ? (int)$matches[1] : (int)($input['id'] ?? $input['file_id'] ?? 0);
        $controller->updateFile($fileId, $input);
    }

    elseif ((preg_match('#^clients/(\d+)/files$#', $path, $matches) || $path === 'clients/files') && $method === 'POST') {
        require_once __DIR__ . '/controllers/ClientController.php';
        $controller = new ClientController();
        $clientId = isset($matches[1]) ? (int)$matches[1] : (int)($input['client_id'] ?? 0);
        $controller->uploadFile($clientId, $input);
    }

    elseif ((preg_match('#^clients/(\d+)/assign$#', $path, $matches) || $path === 'clients/assign') && $method === 'POST') {
        require_once __DIR__ . '/controllers/ClientController.php';
        $controller = new ClientController();
        $clientId = isset($matches[1]) ? (int)$matches[1] : (int)($input['client_id'] ?? 0);
        $controller->assignAgents($clientId, $input);
    }

    // Client Script Routes
    elseif (preg_match('#^clients/(\d+)/script$#', $path, $matches)) {
        require_once __DIR__ . '/controllers/ClientController.php';
        $controller = new ClientController();
        $clientId = (int)$matches[1];
        if ($method === 'GET') {
            $controller->getScript($clientId);
        } elseif ($method === 'POST') {
            $controller->saveScript($clientId, $input);
        } elseif ($method === 'DELETE') {
            $controller->deleteScript($clientId);
        }
    }

    elseif (preg_match('#^clients/(\d+)/script/delete$#', $path, $matches) && $method === 'POST') {
        require_once __DIR__ . '/controllers/ClientController.php';
        $controller = new ClientController();
        $controller->deleteScript((int)$matches[1]);
    }

    elseif ((preg_match('#^clients/(\d+)$#', $path, $matches) && $method === 'DELETE') || ($path === 'clients/delete' && $method === 'POST')) {
        require_once __DIR__ . '/controllers/ClientController.php';
        $controller = new ClientController();
        $clientId = isset($matches[1]) ? (int)$matches[1] : (int)($input['id'] ?? $input['client_id'] ?? 0);
        $controller->delete($clientId);
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
