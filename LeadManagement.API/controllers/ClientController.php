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
        $agentId = isset($_GET['agent_id']) ? (int)$_GET['agent_id'] : (isset($_GET['user_id']) ? (int)$_GET['user_id'] : null);
        if ($agentId && $agentId > 0) {
            $stmt = $this->db->prepare("
                SELECT c.id, c.first_name, c.last_name, c.script_name, c.script_size, c.script_type, c.script_updated_at, c.created_at, c.updated_at
                FROM `clients` c
                INNER JOIN `client_agents` ca ON c.id = ca.client_id
                WHERE ca.user_id = ?
                ORDER BY c.id DESC
            ");
            $stmt->execute([$agentId]);
        } else {
            $stmt = $this->db->query("
                SELECT id, first_name, last_name, script_name, script_size, script_type, script_updated_at, created_at, updated_at
                FROM `clients`
                ORDER BY `id` DESC
            ");
        }
        $clients = $stmt->fetchAll();

        // Attach files and assigned agents for each client
        foreach ($clients as &$client) {
            $clientId = (int) $client['id'];
            $client['full_name'] = trim($client['first_name'] . ' ' . $client['last_name']);
            $client['has_script'] = !empty($client['script_name']);

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

            // 1. Create client_files entry
            $stmtFile = $this->db->prepare("
                INSERT INTO `client_files` 
                (`client_id`, `client_name`, `file_name`, `file_size`, `total_leads`, `batch_id`)
                VALUES (?, ?, ?, ?, ?, NULL)
            ");
            $stmtFile->execute([$clientId, $clientName, $fileName, $fileSize, $totalLeads]);
            $fileId = (int) $this->db->lastInsertId();

            // 2. Insert into dedicated client_leads table
            $stmtLead = $this->db->prepare("
                INSERT INTO `client_leads` (
                    `client_id`, `file_id`, `call_date`, `call_disposition`, 
                    `assigned_agent_id`, `assigned_agent`, `notes`, `call_count`,
                    `status`, `business_name`, `address`, 
                    `city`, `county`, `state`, `zip_code`, `cell_number`, 
                    `fax`, `email`, `annual_sales`, `number_of_employees`, `title`, `contact_name`, 
                    `website`, `sic_code`, `sic_description`, `naics_code`, `naics_description`
                ) VALUES (
                    ?, ?, ?, ?, 
                    ?, ?, ?, ?,
                    ?, ?, ?, 
                    ?, ?, ?, ?, ?, 
                    ?, ?, ?, ?, ?, ?, 
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

                $callDate = !empty($lead['call_date']) ? $lead['call_date'] : null;
                $callDisp = trim($lead['call_disposition'] ?? $lead['call_composition'] ?? '');
                $agentId = !empty($lead['assigned_agent_id']) ? (int)$lead['assigned_agent_id'] : null;
                $agent = trim($lead['assigned_agent'] ?? $lead['agent'] ?? '');
                $notes = trim($lead['notes'] ?? '');
                $callCount = isset($lead['call_count']) && $lead['call_count'] !== '' ? (int)$lead['call_count'] : 1;
                $email = trim($lead['email'] ?? '');

                $stmtLead->execute([
                    $clientId,
                    $fileId,
                    $callDate,
                    $callDisp,
                    $agentId,
                    $agent,
                    $notes,
                    $callCount,
                    $status,
                    $bName,
                    trim($lead['address'] ?? ''),
                    trim($lead['city'] ?? ''),
                    trim($lead['county'] ?? ''),
                    trim($lead['state'] ?? 'NE'),
                    trim($lead['zip_code'] ?? ''),
                    trim($lead['cell_number'] ?? ''),
                    trim($lead['fax'] ?? ''),
                    $email,
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

            // Update total leads count in client_files
            $updFile = $this->db->prepare("UPDATE `client_files` SET `total_leads` = ? WHERE `id` = ?");
            $updFile->execute([$inserted, $fileId]);

            $this->db->commit();

            Response::success([
                'id'          => $fileId,
                'client_id'   => $clientId,
                'client_name' => $clientName,
                'file_name'   => $fileName,
                'file_size'   => $fileSize,
                'total_leads' => $inserted,
                'batch_id'    => null,
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

            // 1. Delete all client_leads belonging to this client
            $delClientLeads = $this->db->prepare("DELETE FROM `client_leads` WHERE `client_id` = ?");
            $delClientLeads->execute([$clientId]);

            // 2. Find any legacy batch IDs belonging to this client's files
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

            // 3. Delete client (cascades client_files and client_agents)
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

            // 1. Delete associated leads from dedicated client_leads table
            $delClientLeads = $this->db->prepare("DELETE FROM `client_leads` WHERE `file_id` = ?");
            $delClientLeads->execute([$fileId]);
            $deletedLeads += $delClientLeads->rowCount();

            // 2. Delete legacy leads if any exist
            if ($batchId) {
                $delLeads = $this->db->prepare("DELETE FROM `leads` WHERE `batch_id` = ?");
                $delLeads->execute([$batchId]);
                $deletedLeads += $delLeads->rowCount();

                $delBatch = $this->db->prepare("DELETE FROM `import_batches` WHERE `id` = ?");
                $delBatch->execute([$batchId]);
            }

            // 3. Delete from client_files
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

        // First check dedicated client_leads table
        $stmtLeads = $this->db->prepare("
            SELECT * FROM `client_leads` 
            WHERE `file_id` = ? 
            ORDER BY `id` ASC
        ");
        $stmtLeads->execute([$fileId]);
        $leads = $stmtLeads->fetchAll();

        // Fallback to legacy leads table if client_leads was empty and batch_id exists
        $batchId = !empty($file['batch_id']) ? (int)$file['batch_id'] : null;
        if (empty($leads) && $batchId) {
            $stmtLegacy = $this->db->prepare("
                SELECT * FROM `leads` 
                WHERE `batch_id` = ? 
                ORDER BY `id` ASC
            ");
            $stmtLegacy->execute([$batchId]);
            $leads = $stmtLegacy->fetchAll();
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

    /**
     * GET /clients/{id}/leads
     * 
     * Retrieves all lead records for a specific client across all their files.
     */
    public function clientLeads(int $clientId): void
    {
        if ($clientId <= 0) {
            Response::error('Invalid client ID.', 422);
        }

        $stmtClient = $this->db->prepare("SELECT id, first_name, last_name FROM `clients` WHERE id = ?");
        $stmtClient->execute([$clientId]);
        $client = $stmtClient->fetch();

        if (!$client) {
            Response::notFound('Client not found.');
        }

        // Query dedicated client_leads table
        $stmtLeads = $this->db->prepare("
            SELECT * FROM `client_leads` 
            WHERE `client_id` = ? 
            ORDER BY `id` ASC
        ");
        $stmtLeads->execute([$clientId]);
        $leads = $stmtLeads->fetchAll();

        // Fallback to legacy leads if empty
        if (empty($leads)) {
            $stmtLegacy = $this->db->prepare("
                SELECT * FROM `leads` 
                WHERE `client_id` = ? 
                ORDER BY `id` ASC
            ");
            $stmtLegacy->execute([$clientId]);
            $leads = $stmtLegacy->fetchAll();
        }

        Response::success([
            'client_id'   => $clientId,
            'client_name' => trim($client['first_name'] . ' ' . $client['last_name']),
            'total'       => count($leads),
            'leads'       => $leads
        ], 'Client leads retrieved successfully');
    }

    /**
     * POST /clients/leads/{id} or POST /clients/leads/update
     * 
     * Updates an individual lead in client_leads.
     */
    public function updateLead(int $leadId, array $input): void
    {
        if ($leadId <= 0) {
            $leadId = (int)($input['id'] ?? 0);
        }
        if ($leadId <= 0) {
            Response::error('Invalid lead ID.', 422);
        }

        $stmt = $this->db->prepare("SELECT id FROM `client_leads` WHERE id = ?");
        $stmt->execute([$leadId]);
        if (!$stmt->fetch()) {
            // Check legacy leads table
            $stmtLegacy = $this->db->prepare("SELECT id FROM `leads` WHERE id = ?");
            $stmtLegacy->execute([$leadId]);
            if (!$stmtLegacy->fetch()) {
                Response::notFound('Lead not found.');
            }
            $table = 'leads';
        } else {
            $table = 'client_leads';
        }

        $allowedFields = [
            'call_date',
            'call_disposition',
            'assigned_agent_id',
            'assigned_agent',
            'notes',
            'call_count',
            'email',
            'status',
            'business_name',
            'address',
            'city',
            'county',
            'state',
            'zip_code',
            'cell_number',
            'fax',
            'annual_sales',
            'number_of_employees',
            'title',
            'contact_name',
            'website',
            'sic_code',
            'sic_description',
            'naics_code',
            'naics_description'
        ];

        $fieldsToUpdate = [];
        $params = [];

        foreach ($allowedFields as $field) {
            if (array_key_exists($field, $input)) {
                $fieldsToUpdate[] = "`{$field}` = ?";
                $params[] = $input[$field];
            }
        }

        if (empty($fieldsToUpdate)) {
            Response::error('No valid fields provided for update.', 422);
        }

        $params[] = $leadId;
        $sql = "UPDATE `{$table}` SET " . implode(', ', $fieldsToUpdate) . " WHERE id = ?";
        $updateStmt = $this->db->prepare($sql);
        $updateStmt->execute($params);

        $fetchStmt = $this->db->prepare("SELECT * FROM `{$table}` WHERE id = ?");
        $fetchStmt->execute([$leadId]);
        $updatedLead = $fetchStmt->fetch();

        Response::success($updatedLead, 'Client lead updated successfully');
    }

    /**
     * GET /clients/{id}/script
     * 
     * Retrieves the uploaded calling script for a specific client.
     */
    public function getScript(int $clientId): void
    {
        if ($clientId <= 0) {
            Response::error('Invalid client ID.', 422);
        }

        $stmt = $this->db->prepare("
            SELECT id, first_name, last_name, script_name, script_size, script_type, script_content, script_updated_at
            FROM `clients`
            WHERE id = ?
        ");
        $stmt->execute([$clientId]);
        $client = $stmt->fetch();

        if (!$client) {
            Response::notFound('Client not found.');
        }

        Response::success([
            'client_id'         => (int)$client['id'],
            'client_name'       => trim($client['first_name'] . ' ' . $client['last_name']),
            'script_name'       => $client['script_name'],
            'script_size'       => $client['script_size'],
            'script_type'       => $client['script_type'],
            'script_content'    => $client['script_content'],
            'script_updated_at' => $client['script_updated_at'],
            'has_script'        => !empty($client['script_name'])
        ], 'Script retrieved successfully.');
    }

    /**
     * POST /clients/{id}/script
     * 
     * Uploads or updates the calling script for a specific client.
     * Body: { "script_name": "...", "script_size": "...", "script_type": "...", "script_content": "..." }
     */
    public function saveScript(int $clientId, array $input): void
    {
        if ($clientId <= 0) {
            Response::error('Invalid client ID.', 422);
        }

        $scriptName = trim($input['script_name'] ?? '');
        $scriptSize = trim($input['script_size'] ?? '');
        $scriptType = strtolower(trim($input['script_type'] ?? ''));
        $scriptContent = $input['script_content'] ?? '';

        if (empty($scriptName)) {
            Response::error('Script name is required.', 422);
        }

        // Verify client exists
        $stmtCheck = $this->db->prepare("SELECT id, first_name, last_name FROM `clients` WHERE id = ?");
        $stmtCheck->execute([$clientId]);
        $client = $stmtCheck->fetch();

        if (!$client) {
            Response::notFound('Client not found.');
        }

        $stmt = $this->db->prepare("
            UPDATE `clients` 
            SET `script_name` = ?, 
                `script_size` = ?, 
                `script_type` = ?, 
                `script_content` = ?, 
                `script_updated_at` = NOW()
            WHERE id = ?
        ");
        $stmt->execute([$scriptName, $scriptSize, $scriptType, $scriptContent, $clientId]);

        Response::success([
            'client_id'         => $clientId,
            'client_name'       => trim($client['first_name'] . ' ' . $client['last_name']),
            'script_name'       => $scriptName,
            'script_size'       => $scriptSize,
            'script_type'       => $scriptType,
            'script_updated_at' => date('Y-m-d H:i:s'),
            'has_script'        => true
        ], "Script '{$scriptName}' saved successfully for client.");
    }

    /**
     * DELETE /clients/{id}/script
     * 
     * Removes the calling script for a specific client.
     */
    public function deleteScript(int $clientId): void
    {
        if ($clientId <= 0) {
            Response::error('Invalid client ID.', 422);
        }

        $stmt = $this->db->prepare("
            UPDATE `clients` 
            SET `script_name` = NULL, 
                `script_size` = NULL, 
                `script_type` = NULL, 
                `script_content` = NULL, 
                `script_updated_at` = NULL
            WHERE id = ?
        ");
        $stmt->execute([$clientId]);

        Response::success(['client_id' => $clientId], 'Script removed successfully.');
    }
}

