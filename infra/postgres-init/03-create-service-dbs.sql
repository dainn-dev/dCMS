-- Per-service database split (microservice "database-per-service" rule).
-- Phase A (DAI-689 follow-up): Voucher + Loyalty.
-- Phase B: Promotions, Fulfillment, Approval, Notification — extracted from dcms_catalog.
CREATE DATABASE dcms_voucher;
CREATE DATABASE dcms_loyalty;
CREATE DATABASE dcms_promotions;
CREATE DATABASE dcms_fulfillment;
CREATE DATABASE dcms_approval;
CREATE DATABASE dcms_notification;
