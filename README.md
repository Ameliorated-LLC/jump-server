<h1 align="center">Jumper</h1>
<h3 align="center">Secure Ncurses-like SSH Jump Server</h3>

---

![Jumper Screenshot](screenshot.png?raw=true)

---

This project provides a secure SSH jump server frontend for Linux systems. Jumper features an intuitive, ncurses-inspired interface that makes setup and management straightforward. It automates environment creation, jump target management, and key imports in a clean, user-friendly terminal interface.

## Deployment

**Prerequisites**

- Debian or Red Hat-based Linux system
- OpenSSH Client (`ssh`) and OpenSSH Server (`sshd`)

### Ubuntu/Debian

For Debian-based systems, see the following installation methods:

#### APT Repository (Recommended)

```bash
curl -s --compressed "https://ameliorated-llc.github.io/jumper/Packaging/PPA/KEY.gpg" | gpg --dearmor | sudo tee /etc/apt/trusted.gpg.d/jumper.gpg >/dev/null
sudo curl -s --compressed -o /etc/apt/sources.list.d/jumper.list "https://ameliorated-llc.github.io/jumper/Packaging/PPA/any.list"
sudo apt update && sudo apt install jumper
```

#### Debian Package

```bash
curl -sL https://github.com/Ameliorated-LLC/jumper/releases/latest/download/jumper.deb -o jumper.deb && sudo dpkg -i jumper.deb && rm jumper.deb
```

### RHEL/Fedora

For Red Hat-based systems, see the following installation methods:

#### RPM Package (WIP)

```bash
curl -sL https://github.com/Ameliorated-LLC/jumper/releases/latest/download/jumper.rpm -o jumper.rpm && sudo rpm -ivh jumper.rpm && rm jumper.rpm
```

## Usage

### Initial Setup

To setup jumper after installation, initialize first time setup by running:

```bash
sudo jumper
```

The setup will:

1. Prompt to set an admin password for future access to the jump server's admin interface.
2. Prompt to set a password for a new `jump` user that will be used with SSH to use jumper.
3. Create the user’s isolated chroot environment and configure `sshd_config` to run jumper with that user.

*If a user named `jump` already exists, it will ask for a username of choice during setup.*

Once setup is complete, SSH into the jump server as the new user:

```bash
ssh jump@localhost
```

Upon logging in, you will be directed to add SSH entries via the secure admin interface.

### Configuration

- Configuration files are located at `/etc/jumper`.

## License

This project is licensed under the MIT License. See the [LICENSE](https://github.com/Ameliorated-LLC/jumper/blob/main/LICENSE) file for more details.