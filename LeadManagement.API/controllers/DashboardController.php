<?php
/**
 * Dashboard Controller
 * 
 * Provides metrics and activity feeds for the Administrator Dashboard.
 */

require_once __DIR__ . '/../core/Database.php';
require_once __DIR__ . '/../core/Response.php';
require_once __DIR__ . '/../core/ActivityLogger.php';

class DashboardController
{
    private PDO $db;

    public function __construct()
    {
        $this->db = Database::getConnection();
    }

    /**
     * GET /dashboard/stats
     * 
     * Computes the total number of leads (leads + client_leads),
     * total number of clients, total number of users,
     * and fetches the latest 10 admin activities.
     */
    public function stats(): void
    {
        try {
            // All leads now live in the centralized `leads` table
            $totalLeads = (int) $this->db->query("SELECT COUNT(*) FROM `leads`")->fetchColumn();
            // Assigned = leads that have been assigned to a client
            $clientLeads = (int) $this->db->query("SELECT COUNT(*) FROM `leads` WHERE client_id IS NOT NULL")->fetchColumn();
            // Legacy client_leads table (kept for backward compat but no longer primary)
            $masterlistLeads = $totalLeads;

            $totalClients = (int) $this->db->query("SELECT COUNT(*) FROM `clients`")->fetchColumn();
            $totalUsers = (int) $this->db->query("SELECT COUNT(*) FROM `users`")->fetchColumn();

            $activities = ActivityLogger::getLatest(10);

            Response::success([
                'total_leads'         => $totalLeads,
                'masterlist_leads'    => $masterlistLeads,
                'client_leads'        => $clientLeads,
                'total_clients'       => $totalClients,
                'total_users'         => $totalUsers,
                'activities'          => $activities,
            ], 'Dashboard metrics retrieved successfully');
        } catch (Exception $e) {
            Response::serverError('Failed to fetch dashboard stats: ' . $e->getMessage());
        }
    }

    /**
     * POST /activities
     * 
     * Allows logging an admin action directly (e.g. from Excel export in desktop client).
     */
    public function logActivity(array $input): void
    {
        $actionType = trim($input['action_type'] ?? 'GENERAL');
        $title = trim($input['title'] ?? 'Admin Action');
        $description = trim($input['description'] ?? '');
        $itemCount = isset($input['item_count']) && $input['item_count'] !== null ? (int)$input['item_count'] : null;
        $adminId = isset($input['admin_id']) ? (int)$input['admin_id'] : null;
        $adminName = trim($input['admin_name'] ?? '');

        if (empty($description)) {
            Response::error('Description is required.', 422);
        }

        $id = ActivityLogger::log($actionType, $title, $description, $itemCount, $adminId, $adminName);

        Response::success([
            'id' => $id,
            'activities' => ActivityLogger::getLatest(10)
        ], 'Activity logged successfully');
    }
}
