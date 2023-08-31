for i in {1..10}; do
  lxc-attach -n n${i} -- bash -c 'echo -e "root\nroot\n" | passwd root';
  lxc-attach -n n${i} -- sed -i 's,^#\?PermitRootLogin .*,PermitRootLogin yes,g' /etc/ssh/sshd_config;
  lxc-attach -n n${i} -- systemctl restart sshd;
done