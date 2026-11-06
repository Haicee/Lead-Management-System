<?php
/**
 * Client Controller
 * 
 * Manages Client records, uploaded files per client, and agent assignments.
 */

class ClientController
{
    private PDO $db;

    public function __construct()
    {
        $this->db = Database::getConnection();
    }

    /**
     * GET /clients
     * 
     * Returns all clients with their files and assigned agents.
     */
    public function index(): void
    {
        $stmt = $this->db->query("
            SELECT id, first_name, last_name, created_at, updated_at
            FROM `clients`
            ORDER BY `id` DESC
        ");
        $clients = $stmt->fetchAll();

        // Attach files and assigned agents for each client
        foreach ($clients as &$client) {
            $clientId = (int) $client['id'];
            $client['full_name'] = trim($client['first_name'] . ' ' . $client['last_name']);

            // 1. Files uploaded for this client
            $stmtFiles = $this->db->prepare("
                SELECT id, client_id, client_name, file_name, file_size, total_leads, batch_id, created_at
                FROM `client_files`
                WHERE `client_id` = ?
                ORDER BY `id` DESC
            ");
            $stmtFiles->execute([$clientId]);
            $files = $stmtFiles->fetchAll();

            $client['files'] = $files;
            $client['file_count'] = count($files);
            $client['total_leads'] = (int) array_sum(array_column($files, 'total_leads'));

            // 2. Assigned agents (Users with role = 'user' assigned to this client)
            $stmtAgents = $this->db->prepare("
                SELECT u.id, u.username, u.full_name, u.email, u.role
                FROM `client_agents` ca
                INNER JOIN `users` u ON ca.user_id = u.id
                WHERE ca.client_id = ?
                ORDER BY u.full_name ASC
            ");
            $stmtAgents->execute([$clientId]);
            $client['assigned_agents'] = $stmtAgents->fetchAll();
        }

        Response::success($clients, 'Clients retrieved successfully');
    }

    /**
     * POST /clients
     * 
     * Adds a new client (First Name and Last Name only).
     * Body: { "first_name": "...", "last_name": "..." }
     */
    public function store(array $input): void
    {
        $firstName = trim($input['first_name'] ?? '');
        $lastName = trim($input['last_name'] ?? '');

        if (empty($firstName)) {
            Response::error('First name is required.', 422);
        }
        if (empty($lastName)) {
            Response::error('Last name is required.', 422);
        }

        $stmt = $this->db->prepare("
            INSERT INTO `clients` (`first_name`, `last_name`)
            VALUES (?, ?)
        ");
        $stmt->execute([$firstName, $lastName]);
        $newId = (int) $this->db->lastInsertId();

        $fullName = trim($firstName . ' ' . $lastName);

        Response::success([
            'id'              => $newId,
            'first_name'      => $firstName,
            'last_name'       => $lastName,
            'full_name'       => $fullName,
            'file_count'      => 0,
            'total_leads'     => 0,
            'files'           => [],
            'assigned_agents' => [],
            'created_at'      => date('Y-m-d H:i:s'),
        ], 'Client created successfully', 201);
    }

    /**
     * POST /clients/{id}/files or POST /clients/files
     * 
     * Uploads and associates a leads file with a specific client.
     * Ingests parsed lead records into the leads and import_batches database tables.
     */
    public function uploadFile(int $clientId, array $input): void
    {
        if ($clientId <= 0) {
            Response::error('Invalid client ID.', 422);
        }

        // Verify client exists
        $stmtClient = $this->db->prepare("SELECT id, first_name, last_name FROM `clients` WHERE id = ?");
        $stmtClient->execute([$clientId]);
        $client = $stmtClient->fetch();

        if (!$client) {
            Response::notFound('Client not found.');
        }

        $clientName = trim($client['first_name'] . ' ' . $client['last_name']);
        $fileName = trim($input['file_name'] ?? 'Uploaded_Leads_' . date('Ymd_His'));
        $fileSize = trim($input['file_size'] ?? '0 KB');
        $leads = $input['leads'] ?? [];

        if (!is_array($leads) || empty($leads)) {
            Response::error('No lead records provided for import.', 422);
        }

        $totalLeads = count($leads);
        $batchName = "Client [{$clientName}] - " . pathinfo($fileName, PATHINFO_FILENAME);

        try {
            $this->db->beginTransaction();

            // 1. Create import_batches audit entry
            $stmtBatch = $this->db->prepare("
                INSERT INTO `import_batches` 
                (`batch_name`, `file_name`, `total_leads`, `initial_status`, `imported_by`) 
                VALUES (?, ?, ?, 'New Lead', NULL)
            ");
            $stmtBatch->execute([$batchName, $fileName, $totalLeads]);
            $batchId = (int) $this->db->lastInsertId();

            // 2. Insert leads records
            $stmtLead = $this->db->prepare("
                INSERT INTO `leads` (
                    `batch_id`, `batch_name`, `status`, `business_name`, `address`, 
                    `city`, `county`, `state`, `zip_code`, `cell_number`, 
                    `fax`, `annual_sales`, `number_of_employees`, `title`, `contact_name`, 
                    `website`, `sic_code`, `sic_description`, `naics_code`, `naics_description`
                ) VALUES (
                    ?, ?, ?, ?, ?, 
                    ?, ?, ?, ?, ?, 
                    ?, ?, ?, ?, ?, 
                    ?, ?, ?, ?, ?
                )
            ");

            $inserted = 0;
            foreach ($leads as $lead) {
                $status = !empty($lead['status']) ? $lead['status'] : 'New Lead';
                $bName = trim($lead['business_name'] ?? '');
                if (empty($bName)) {
                    $bName = 'Unnamed Lead #' . ($inserted + 1);
                }

                $stmtLead->execute([
                    $batchId,
                    $batchName,
                    $status,
                    $bName,
                    trim($lead['address'] ?? ''),
                    trim($lead['city'] ?? ''),
                    trim($lead['county'] ?? ''),
                    trim($lead['state'] ?? 'NE'),
                    trim($lead['zip_code'] ?? ''),
                    trim($lead['cell_number'] ?? ''),
                    trim($lead['fax'] ?? ''),
                    trim($lead['annual_sales'] ?? 'Less than 500,000'),
                    trim($lead['number_of_employees'] ?? '1 to 4'),
                    trim($lead['title'] ?? ''),
                    trim($lead['contact_name'] ?? ''),
                    trim($lead['website'] ?? ''),
                    trim($lead['sic_code'] ?? ''),
                    trim($lead['sic_description'] ?? ''),
                    trim($lead['naics_code'] ?? ''),
                    trim($lead['naics_description'] ?? '')
                ]);
                $inserted++;
            }

            // 3. Create client_files entry
            $stmtFile = $this->db->prepare("
                INSERT INTO `client_files` 
                (`client_id`, `client_name`, `file_name`, `file_size`, `total_leads`, `batch_id`)
                VALUES (?, ?, ?, ?, ?, ?)
            ");
            $stmtFile->execute([$clientId, $clientName, $fileName, $fileSize, $inserted, $batchId]);
            $fileId = (int) $this->db->lastInsertId();

            $this->db->commit();

            Response::success([
                'id'          => $fileId,
                'client_id'   => $clientId,
                'client_name' => $clientName,
                'file_name'   => $fileName,
                'file_size'   => $fileSize,
                'total_leads' => $inserted,
                'batch_id'    => $batchId,
                'created_at'  => date('Y-m-d H:i:s'),
            ], "Successfully uploaded {$inserted} leads for client '{$clientName}'.", 201);

        } catch (Exception $e) {
            if ($this->db->inTransaction()) {
                $this->db->rollBack();
            }
            Response::serverError('Failed to upload file for client: ' . $e->getMessage());
        }
    }

    /**
     * POST /clients/{id}/assign or POST /clients/assign
     * 
     * Assigns agent users (role = 'user') to a client.
     * Body: { "agent_ids": [2, 3] }
     */
    public function assignAgents(int $clientId, array $input): void
    {
        if ($clientId <= 0) {
            Response::error('Invalid client ID.', 422);
        }

        $agentIds = $input['agent_ids'] ?? [];
        if (!is_array($agentIds)) {
            $agentIds = [];
        }

        try {
            $this->db->beginTransaction();

            // Clear previous assignments for this client
            $del = $this->db->prepare("DELETE FROM `client_agents` WHERE `client_id` = ?");
            $del->execute([$clientId]);

            // Insert new assignments
            if (!empty($agentIds)) {
                $ins = $this->db->prepare("INSERT INTO `client_agents` (`client_id`, `user_id`) VALUES (?, ?)");
                foreach ($agentIds as $agentId) {
                    $ins->execute([$clientId, (int) $agentId]);
                }
            }

            $this->db->commit();

            // Retrieve updated assigned agents
            $stmt = $this->db->prepare("
                SELECT u.id, u.username, u.full_name, u.email, u.role
                FROM `client_agents` ca
                INNER JOIN `users` u ON ca.user_id = u.id
                WHERE ca.client_id = ?
                ORDER BY u.full_name ASC
            ");
            $stmt->execute([$clientId]);
            $updatedAgents = $stmt->fetchAll();

            Response::success($updatedAgents, 'Agent assignments updated successfully.');

        } catch (Exception $e) {
            if ($this->db->inTransaction()) {
                $this->db->rollBack();
            }
            Response::serverError('Failed to update agent assignments: ' . $e->getMessage());
        }
    }

    /**
     * DELETE /clients/{id} or POST /clients/delete
     * 
     * Deletes a client and cascades to their files, leads, and assignments.
     */
    public function delete(int $clientId): void
    {
        if ($clientId <= 0) {
            Response::error('Invalid client ID.', 422);
        }

        try {
            $this->db->beginTransaction();

            // 1. Find all batch IDs belonging to this client's files
            $stmtFiles = $this->db->prepare("SELECT id, batch_id FROM `client_files` WHERE `client_id` = ?");
            $stmtFiles->execute([$clientId]);
            $files = $stmtFiles->fetchAll();

            foreach ($files as $f) {
                if (!empty($f['batch_id'])) {
                    $batchId = (int)$f['batch_id'];
                    $delLeads = $this->db->prepare("DELETE FROM `leads` WHERE `batch_id` = ?");
                    $delLeads->execute([$batchId]);

                    $delBatch = $this->db->prepare("DELETE FROM `import_batches` WHERE `id` = ?");
                    $delBatch->execute([$batchId]);
                }
            }

            // 2. Delete client (cascades client_files and client_agents)
            $stmt = $this->db->prepare("DELETE FROM `clients` WHERE `id` = ?");
            $stmt->execute([$clientId]);

            $this->db->commit();

            Response::success(['id' => $clientId], 'Client and all associated files deleted successfully.');
        } catch (Exception $e) {
            if ($this->db->inTransaction()) {
                $this->db->rollBack();
            }
            Response::serverError('Failed to delete client: ' . $e->getMessage());
        }
    }

    /**
     * DELETE /clients/files/{id} or POST /clients/files/delete
     * 
     * Deletes a specific client file and all leads imported with it.
     */
    public function deleteFile(int $fileId): void
    {
        if ($fileId <= 0) {
            Response::error('Invalid file ID.', 422);
        }

        $stmt = $this->db->prepare("SELECT id, client_id, client_name, file_name, batch_id, total_leads FROM `client_files` WHERE id = ?");
        $stmt->execute([$fileId]);
        $file = $stmt->fetch();

        if (!$file) {
            Response::notFound('Client file not found.');
        }

        try {
            $this->db->beginTransaction();

            $batchId = !empty($file['batch_id']) ? (int)$file['batch_id'] : null;
            $deletedLeads = 0;

            if ($batchId) {
                // Delete associated leads
                $delLeads = $this->db->prepare("DELETE FROM `leads` WHERE `batch_id` = ?");
                $delLeads->execute([$batchId]);
                $deletedLeads = $delLeads->rowCount();

                // Delete associated import batch
                $delBatch = $this->db->prepare("DELETE FROM `import_batches` WHERE `id` = ?");
                $delBatch->execute([$batchId]);
            }

            // Delete from client_files
            $delFile = $this->db->prepare("DELETE FROM `client_files` WHERE `id` = ?");
            $delFile->execute([$fileId]);

            $this->db->commit();

            Response::success([
                'id'            => $fileId,
                'client_id'     => $file['client_id'],
                'file_name'     => $file['file_name'],
                'batch_id'      => $batchId,
                'deleted_leads' => $deletedLeads
            ], "File '{$file['file_name']}' and its leads were deleted successfully.");

        } catch (Exception $e) {
            if ($this->db->inTransaction()) {
                $this->db->rollBack();
            }
            Response::serverError('Failed to delete client file: ' . $e->getMessage());
        }
    }

    /**
     * GET /clients/files/{id}/leads
     * 
     * Retrieves all lead records imported under a specific client file.
     */
    public function fileLeads(int $fileId): void
    {
        if ($fileId <= 0) {
            Response::error('Invalid file ID.', 422);
        }

        $stmt = $this->db->prepare("SELECT id, client_id, client_name, file_name, file_size, batch_id, total_leads, created_at FROM `client_files` WHERE id = ?");
        $stmt->execute([$fileId]);
        $file = $stmt->fetch();

        if (!$file) {
            Response::notFound('Client file not found.');
        }

        $batchId = !empty($file['batch_id']) ? (int)$file['batch_id'] : null;
        $leads = [];

        if ($batchId) {
            $stmtLeads = $this->db->prepare("
                SELECT * FROM `leads` 
                WHERE `batch_id` = ? 
                ORDER BY `id` ASC
            ");
            $stmtLeads->execute([$batchId]);
            $leads = $stmtLeads->fetchAll();
        }

        Response::success([
            'file_id'     => $fileId,
            'client_id'   => (int)$file['client_id'],
            'client_name' => $file['client_name'],
            'file_name'   => $file['file_name'],
            'file_size'   => $file['file_size'],
            'batch_id'    => $batchId,
            'created_at'  => $file['created_at'],
            'total'       => count($leads),
            'leads'       => $leads
        ], 'File leads retrieved successfully');
    }

    /**
     * PUT /clients/files/{id} or POST /clients/files/update
     * 
     * Updates client file information (e.g. rename file).
     */
    public function updateFile(int $fileId, array $input): void
    {
        if ($fileId <= 0) {
            Response::error('Invalid file ID.', 422);
        }

        $fileName = trim($input['file_name'] ?? '');
        if (empty($fileName)) {
            Response::error('File name is required.', 422);
        }

        $stmt = $this->db->prepare("SELECT id, file_name FROM `client_files` WHERE id = ?");
        $stmt->execute([$fileId]);
        $file = $stmt->fetch();

        if (!$file) {
            Response::notFound('Client file not found.');
        }

        $upd = $this->db->prepare("UPDATE `client_files` SET `file_name` = ? WHERE `id` = ?");
        $upd->execute([$fileName, $fileId]);

        Response::success([
            'id'        => $fileId,
            'file_name' => $fileName
        ], 'File updated successfully.');
    }
}

