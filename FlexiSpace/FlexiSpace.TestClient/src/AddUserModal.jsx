import React, { useState, useEffect } from "react";

export const AddUserModal = ({ isOpen, onClose, accessToken, locations, onUserCreated }) => {
  const [unprovisionedUsers, setUnprovisionedUsers] = useState([]);
  const [selectedUser, setSelectedUser] = useState(null);
  const [selectedLocationId, setSelectedLocationId] = useState("");
  const [selectedRole, setSelectedRole] = useState("Staff");
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  // Load unprovisioned Entra users when modal opens
  useEffect(() => {
    if (isOpen) {
      loadUnprovisionedUsers();
    } else {
      resetForm();
    }
  }, [isOpen]);

  const loadUnprovisionedUsers = async () => {
    setLoading(true);
    setError("");
    try {
      const response = await fetch("/api/User/unprovisioned", {
        headers: {
          Authorization: `Bearer ${accessToken}`,
        },
      });

      if (!response.ok) {
        throw new Error("Failed to load Entra users.");
      }

      const data = await response.json();
      setUnprovisionedUsers(data);
    } catch (err) {
      setError(err.message || "An error occurred while fetching users.");
    } finally {
      setLoading(false);
    }
  };

  const handleUserSelect = (e) => {
    const entraId = e.target.value;
    const user = unprovisionedUsers.find((u) => u.entraObjectId === entraId);
    setSelectedUser(user || null);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!selectedUser || !selectedLocationId) {
      setError("Please select both an Entra user and a location.");
      return;
    }

    setSubmitting(true);
    setError("");

    const payload = {
      entraObjectId: selectedUser.entraObjectId,
      firstName: selectedUser.firstName,
      lastName: selectedUser.lastName,
      email: selectedUser.email,
      locationId: parseInt(selectedLocationId, 10),
      role: selectedRole,
    };

    try {
      const response = await fetch("/api/User", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${accessToken}`,
        },
        body: JSON.stringify(payload),
      });

      if (!response.ok) {
        throw new Error("Failed to provision user in FlexiSpace.");
      }

      const newUser = await response.json();
      onUserCreated(newUser);
      onClose();
    } catch (err) {
      setError(err.message || "Failed to create user.");
    } finally {
      setSubmitting(false);
    }
  };

  const resetForm = () => {
    setSelectedUser(null);
    setSelectedLocationId("");
    setSelectedRole("Staff");
    setError("");
  };

  if (!isOpen) return null;

  return (
    <div style={styles.overlay}>
      <div style={styles.modal}>
        <h2>Provision New Entra User</h2>
        {error && <p style={styles.error}>{error}</p>}

        {loading ? (
          <p>Loading unprovisioned Entra users...</p>
        ) : (
          <form onSubmit={handleSubmit}>
            {/* Entra User Selector */}
            <div style={styles.field}>
              <label>Select Entra ID User:</label>
              <select
                value={selectedUser?.entraObjectId || ""}
                onChange={handleUserSelect}
                required
              >
                <option value="">-- Select Entra User --</option>
                {unprovisionedUsers.map((u) => (
                  <option key={u.entraObjectId} value={u.entraObjectId}>
                    {u.firstName} {u.lastName} ({u.email})
                  </option>
                ))}
              </select>
            </div>

            {/* Read-only Auto-populated Details */}
            {selectedUser && (
              <div style={styles.readOnlyBox}>
                <p><strong>First Name:</strong> {selectedUser.firstName}</p>
                <p><strong>Last Name:</strong> {selectedUser.lastName}</p>
                <p><strong>Email:</strong> {selectedUser.email}</p>
                <p><strong>Entra Object ID:</strong> {selectedUser.entraObjectId}</p>
              </div>
            )}

            {/* FlexiSpace Location */}
            <div style={styles.field}>
              <label>Assigned Location:</label>
              <select
                value={selectedLocationId}
                onChange={(e) => setSelectedLocationId(e.target.value)}
                required
              >
                <option value="">-- Select Location --</option>
                {locations?.map((loc) => (
                  <option key={loc.id} value={loc.id}>
                    {loc.name}
                  </option>
                ))}
              </select>
            </div>

            {/* Application Role */}
            <div style={styles.field}>
              <label>FlexiSpace Role:</label>
              <select
                value={selectedRole}
                onChange={(e) => setSelectedRole(e.target.value)}
              >
                <option value="Staff">Staff</option>
                <option value="CentreManager">Centre Manager</option>
                <option value="Administrator">Administrator</option>
              </select>
            </div>

            {/* Form Actions */}
            <div style={styles.actions}>
              <button type="button" onClick={onClose} disabled={submitting}>
                Cancel
              </button>
              <button type="submit" disabled={submitting || !selectedUser}>
                {submitting ? "Saving..." : "Provision User"}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
};

// Basic inline styling
const styles = {
  overlay: {
    position: "fixed",
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    backgroundColor: "rgba(0,0,0,0.5)",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    zIndex: 1000,
  },
  modal: {
    background: "#fff",
    padding: "24px",
    borderRadius: "8px",
    width: "480px",
    maxWidth: "90%",
  },
  field: {
    display: "flex",
    flexDirection: "column",
    gap: "6px",
    marginBottom: "16px",
  },
  readOnlyBox: {
    background: "#f4f4f5",
    padding: "12px",
    borderRadius: "6px",
    fontSize: "0.9rem",
    marginBottom: "16px",
  },
  actions: {
    display: "flex",
    justifyContent: "flex-end",
    gap: "12px",
    marginTop: "20px",
  },
  error: {
    color: "#e11d48",
    fontSize: "0.875rem",
  },
};