import { Injectable, inject, signal, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import * as signalR from '@microsoft/signalr';

export interface OrderStatusUpdate {
  orderId: string;
  status: string;
  timestamp: string;
}

export interface OrderJourneyEvent {
  displayOrderId: string;
  components: string[];
  eventName: string;
  messages: string[];
  timestamp: string;
}

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private hubConnection!: signalR.HubConnection;
  private platformId = inject(PLATFORM_ID);
  private started = false;

  // Angular Signal to hold real-time order updates
  public latestUpdate = signal<OrderStatusUpdate | null>(null);
  public connectionState = signal<string>('Initializing');

  // Per-order status map keyed by orderId
  public orderStatuses = signal<Record<string, OrderStatusUpdate>>({});

  // Order journey event log (ascending — oldest first)
  public orderJourneyEvents = signal<OrderJourneyEvent[]>([]);

  public startConnection(): void {
    if (!isPlatformBrowser(this.platformId) || this.started) {
      return;
    }
    this.started = true;

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/order')
      .withAutomaticReconnect()
      .build();

    const timeoutHandle = setTimeout(() => {
      if (this.connectionState() === 'Initializing') {
        this.connectionState.set('Disconnected');
        console.warn('SignalR: no ServerReady received within 60 seconds.');
      }
    }, 60_000);

    this.registerServerReadyListener(timeoutHandle);
    this.registerOrderStateListener();
    this.registerOrderJourneyListener();

    this.hubConnection.start()
      .catch(err => console.error('SignalR connection error:', err));
  }


  private registerServerReadyListener(timeoutHandle: ReturnType<typeof setTimeout>): void {
    this.hubConnection.on('ServerReady', () => {
      clearTimeout(timeoutHandle);
      this.connectionState.set('Connected');
      console.log('SignalR: ServerReady received — API is up.');
    });
  }

  private registerOrderStateListener(): void {
    this.hubConnection.on('ReceiveOrderUpdate', (update: OrderStatusUpdate) => {
      this.latestUpdate.set(update);
      this.orderStatuses.update(current => ({ ...current, [update.orderId]: update }));
    });
  }

  private registerOrderJourneyListener(): void {
    this.hubConnection.on('ReceiveOrderJourneyEvent', (event: OrderJourneyEvent) => {
      this.orderJourneyEvents.update(events => [...events, event]);
    });
  }

  public clearJourneyEvents(): void {
    this.orderJourneyEvents.set([]);
  }
}

