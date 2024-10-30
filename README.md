# Secure SSH Jump Server

This project provides a secure SSH jump server for Debian and RedHat-based systems, designed for users who want a clean interface and automated SSH management. The jump server generates its own SSH key that optionally copies the public key to remote `authorized_keys` files when adding an SSH server entry. This allows secure access to remote servers from an isolated, controlled environment. During initial setup, it creates a chroot environment under a new user, and facilitates adding entries through a secure admin interface.

## Features

- **Isolated Chroot Environment**: Creates an isolated chroot environment, reducing exposure and maintaining security.
- **Automated SSH Key Management**: Automatically generates and manages an SSH key, and copies the public key to added remote servers.
- **Automated Server Setup**: Automates adding remote servers, optionally disabling password SSH authentication after copying the public key.

## Installation

### Prerequisites

- Debian or RedHat-based Linux system
- `wget` package for fetching the latest release from GitHub

### Deployment

Download and install the latest release from the [GitHub repository](https://github.com/Ameliorated-LLC/jump-server), using the correct file for your system type.

For Debian-based systems, use the following command:

```bash
wget -q https://github.com/Ameliorated-LLC/jump-server/releases/latest/download/jump-server.deb && sudo dpkg -i jump-server.deb && rm jump-server.deb
```

For RedHat-based systems, use the following command:

```bash
wget -q https://github.com/Ameliorated-LLC/jump-server/releases/latest/download/jump-server.rpm && sudo rpm -ivh jump-server.rpm && rm jump-server.rpm
```

## Initial Setup

After installation, initialize first time setup by running:

```bash
sudo jump-server
```

The setup will:

1. Prompt to set an admin password for future access to the jump server's admin interface.
2. Prompt to set a password for a new `jump` user that will be used with SSH to use jump-server.
3. Create the user’s isolated chroot environment and configure SSH config to run jump-server with that user.

*If a user named `jump` already exists, it will ask for a username of choice during setup.*

Once setup is complete, SSH into the jump server as the new user:

```bash
ssh jump@localhost
```

Upon logging in, you will be directed to add SSH entries via the secure admin interface.

## Usage

### Admin Interface

- Use the jump server’s admin interface to add and manage SSH entries.
- To access the admin interface, press Ctrl + X in the jump menu and enter the admin password set during initial setup.

## Configuration

- User-specific configuration files are located at `/etc/jump-server`.

## License

This project is licensed under the MIT License. See the [LICENSE](https://github.com/Ameliorated-LLC/jump-server/blob/main/LICENSE) file for more details.