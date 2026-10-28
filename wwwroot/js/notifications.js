// Browser-Benachrichtigungen (nur solange Syncro geöffnet ist)
window.syncroNotifications = {
    permission: () => ('Notification' in window) ? Notification.permission : 'unsupported',
    request: async () => ('Notification' in window) ? await Notification.requestPermission() : 'unsupported',
    show: (title, body) => {
        if ('Notification' in window && Notification.permission === 'granted') {
            new Notification(title, { body: body, icon: 'icon-192.png' });
        }
    }
};
