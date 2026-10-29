<?php
/**
 * JSON Response Helper
 * 
 * Standardized JSON responses for the REST API.
 */

class Response
{
    /**
     * Send a success response.
     */
    public static function success(mixed $data = null, string $message = 'Success', int $statusCode = 200): void
    {
        http_response_code($statusCode);
        header('Content-Type: application/json; charset=utf-8');
        echo json_encode([
            'success' => true,
            'message' => $message,
            'data'    => $data,
        ], JSON_UNESCAPED_UNICODE);
        exit;
    }

    /**
     * Send an error response.
     */
    public static function error(string $message = 'An error occurred', int $statusCode = 400, mixed $errors = null): void
    {
        http_response_code($statusCode);
        header('Content-Type: application/json; charset=utf-8');
        $payload = [
            'success' => false,
            'message' => $message,
        ];
        if ($errors !== null) {
            $payload['errors'] = $errors;
        }
        echo json_encode($payload, JSON_UNESCAPED_UNICODE);
        exit;
    }

    /**
     * Send a 401 Unauthorized response.
     */
    public static function unauthorized(string $message = 'Unauthorized'): void
    {
        self::error($message, 401);
    }

    /**
     * Send a 403 Forbidden response.
     */
    public static function forbidden(string $message = 'Forbidden'): void
    {
        self::error($message, 403);
    }

    /**
     * Send a 404 Not Found response.
     */
    public static function notFound(string $message = 'Resource not found'): void
    {
        self::error($message, 404);
    }

    /**
     * Send a 500 Internal Server Error response.
     */
    public static function serverError(string $message = 'Internal server error'): void
    {
        self::error($message, 500);
    }
}
