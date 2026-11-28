self.addEventListener('push', event => {
  let data = {};

  try {
    data = event.data
      ? event.data.json()
      : {};
  } catch {
    data = {};
  }

  const title =
    data.title || 'CrisisAid notification';

  const options = {
    body:
      data.body ||
      'You have a new CrisisAid update.',
    tag: data.eventKey || 'crisisaid-update',
    data: {
      url: '/'
    }
  };

  event.waitUntil(
    self.registration.showNotification(
      title,
      options
    )
  );
});

self.addEventListener(
  'notificationclick',
  event => {
    event.notification.close();

    event.waitUntil(
      clients.openWindow('/')
    );
  }
);