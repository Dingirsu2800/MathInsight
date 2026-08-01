import * as React from 'react';
import MaterialIcon from './ui/MaterialIcon';
import { Dialog, DialogHeader, DialogTitle, DialogContent } from './ui/dialog';
import { getAccessToken } from '../services/authStorage';
import { getNotifications, markNotificationRead, connectNotificationHub } from '../services/notificationApi';

function formatRelativeTime(isoDate) {
  const diffMs = Date.now() - new Date(isoDate).getTime();
  const minutes = Math.floor(diffMs / 60000);
  if (minutes < 1) return 'Vừa xong';
  if (minutes < 60) return `${minutes} phút trước`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} giờ trước`;
  return `${Math.floor(hours / 24)} ngày trước`;
}

function formatFullTime(isoDate) {
  return new Date(isoDate).toLocaleString('vi-VN', {
    dateStyle: 'long',
    timeStyle: 'short'
  });
}

/**
 * Real-time notification bell — mounted in DashboardTopbar for every authenticated role.
 * Loads the recent unread list on mount, then subscribes to the /hubs/notification SignalR
 * push channel (BR-20/BR-22) for live updates while the dropdown is closed or open.
 */
export default function NotificationBell() {
  const [notifications, setNotifications] = React.useState([]);
  const [unreadCount, setUnreadCount] = React.useState(0);
  const [isOpen, setIsOpen] = React.useState(false);
  const [selectedNotification, setSelectedNotification] = React.useState(null);
  const menuRef = React.useRef(null);

  React.useEffect(() => {
    if (!getAccessToken()) return undefined;

    let stopHub = null;
    let cancelled = false;

    getNotifications({ pageSize: 10 })
      .then((data) => {
        if (cancelled) return;
        const items = data?.items || [];
        setNotifications(items);
        setUnreadCount(items.filter((n) => !n.isRead).length);
      })
      .catch(() => {
        // No history available yet — the bell still works for live pushes.
      });

    connectNotificationHub((notification) => {
      setNotifications((prev) => [notification, ...prev].slice(0, 10));
      setUnreadCount((prev) => prev + 1);
    })
      .then((stop) => {
        if (cancelled) {
          stop();
        } else {
          stopHub = stop;
        }
      })
      .catch(() => {
        // Real-time push unavailable (e.g. hub unreachable) — history above still loaded.
      });

    return () => {
      cancelled = true;
      if (stopHub) stopHub();
    };
  }, []);

  React.useEffect(() => {
    function handleClickOutside(event) {
      if (menuRef.current && !menuRef.current.contains(event.target)) {
        setIsOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  function handleSelect(notification) {
    if (!notification.isRead) {
      setNotifications((prev) =>
        prev.map((n) => (n.notificationId === notification.notificationId ? { ...n, isRead: true } : n))
      );
      setUnreadCount((prev) => Math.max(0, prev - 1));
      markNotificationRead(notification.notificationId).catch(() => {});
    }

    setIsOpen(false);
    setSelectedNotification(notification);
  }

  return (
    <div className="relative flex items-center" ref={menuRef}>
      <button
        type="button"
        onClick={() => setIsOpen((prev) => !prev)}
        className="relative p-2 rounded-full text-on-surface-variant hover:bg-surface-container transition-colors cursor-pointer border-0 bg-transparent outline-none"
        aria-label="Thông báo"
      >
        <span className="material-symbols-outlined">notifications</span>
        {unreadCount > 0 && (
          <span className="absolute top-0 right-0 min-w-[16px] h-4 px-1 rounded-full bg-error text-white text-[10px] font-bold flex items-center justify-center">
            {unreadCount > 9 ? '9+' : unreadCount}
          </span>
        )}
      </button>

      {isOpen && (
        <div className="absolute right-0 top-full mt-2 w-80 rounded-xl bg-pure-surface border border-whisper-border diffused-shadow p-2 z-[60] flex flex-col gap-1 text-[13px] max-h-96 overflow-y-auto">
          <div className="px-3 py-2 border-b border-whisper-border">
            <p className="font-bold text-on-surface">Thông báo</p>
          </div>

          {notifications.length === 0 ? (
            <p className="px-3 py-4 text-center text-on-surface-variant">Chưa có thông báo nào.</p>
          ) : (
            notifications.map((notification) => (
              <button
                key={notification.notificationId}
                type="button"
                onClick={() => handleSelect(notification)}
                className={`flex flex-col items-start gap-0.5 px-3 py-2 rounded-lg text-left w-full cursor-pointer border-0 outline-none transition-colors ${
                  notification.isRead ? 'bg-transparent' : 'bg-primary/5'
                } hover:bg-surface-container`}
              >
                <span className="font-semibold text-on-surface">{notification.title}</span>
                <span className="text-on-surface-variant">{notification.content}</span>
                <span className="text-[11px] text-outline">{formatRelativeTime(notification.createdTime)}</span>
              </button>
            ))
          )}
        </div>
      )}

      <Dialog isOpen={!!selectedNotification} onClose={() => setSelectedNotification(null)}>
        {selectedNotification && (
          <>
            <DialogHeader>
              <DialogTitle>{selectedNotification.title}</DialogTitle>
            </DialogHeader>
            <DialogContent>
              <p className="whitespace-pre-wrap text-on-surface">{selectedNotification.content}</p>
              <p className="mt-3 text-[12px] text-outline">{formatFullTime(selectedNotification.createdTime)}</p>
            </DialogContent>
          </>
        )}
      </Dialog>
    </div>
  );
}
