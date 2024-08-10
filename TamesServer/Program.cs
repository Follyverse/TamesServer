using TamesServer;

Registry reg = new Registry();
OwnerSide storage = new OwnerSide() { username = reg.email, admin = reg.name, password = reg.password };
Logs logs = new Logs();
Server server = new Server(reg.ip, reg.port);