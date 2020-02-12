import logging
import sys
import os

from datetime import date, datetime, timedelta, timezone

import azure.functions as func
from sqlalchemy import func as sqlfunc

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '..', 'shared')))

from shared.alerter import *
from shared.db_engine import DbEngine
from shared.sqla_objects import *

db = DbEngine()

def process():
    session = db.get_session()

    # 1. Обработка профилей которые уже есть в базе данных.
    results = session.query(SearchResult,
                            ClientCandidates.id).outerjoin(ClientCandidates, (ClientCandidates.candidate_external_id == SearchResult.candidate_external_id) & \
                                                                             (ClientCandidates.client_id == SearchResult.client_id)) \
                                                .filter(SearchResult.processed == False) \
                                                .limit(100).all()
    for result in results:
        # Списание за найденного кандидата
        charge = Charges()
        charge.client_id = result.SearchResult.client_id
        charge.transaction_type_id = 1 # Начисление за услугу
        charge.creation_date = datetime.now()

        # TODO: убрать хардкод стоимости услуги
        if result.id is None:
            # Запись в БД информации о том, что найден кандидат для организации
            client_candidate = ClientCandidates()
            client_candidate.client_id = result.SearchResult.client_id
            client_candidate.candidate_profile_id = result.SearchResult.candidate_profile_id
            client_candidate.candidate_external_id = result.SearchResult.candidate_external_id
            client_candidate.creation_date = datetime.now()
            client_candidate.billed = False

            # Кандидат уже находился в рамках текущей организации
            res = session.query(ClientCandidates.id).filter(ClientCandidates.candidate_external_id == result.SearchResult.candidate_external_id) \
                                                    .filter(ClientCandidates.client_id != result.SearchResult.client_id).first()
            if res is None:
                # Кандидат находится впервые
                client_candidate.status = "NEW"
                charge.amount = 4
            else:
                # Кандидат найден у другой организации
                client_candidate.status = "REFOUND"
                charge.amount = 2
            session.add(client_candidate)
        else:
            charge.amount = 0

        session.add(charge)
        result.SearchResult.processed = True

    try:
        session.commit()
    except Exception:
        logging.warning("Запись результатов обработки профилей со статусом 'SKIPPED'. Обработано: {}".format(len(results)))
        session.rollback()
    finally:
        session.close()

def process_balance_changing():
    session = db.get_session()

    clients = session.query(Clients.id).all()
    for client in clients:
        last_balance = session.query(Balances.creation_date,
                                     Balances.amount).filter(Balances.client_id == client.id).order_by(Balances.creation_date.desc()).one()

        last_charges = session.query(sqlfunc.sum(Charges.amount).label("amount")) \
                              .filter(Charges.client_id == client.id) \
                              .filter(Charges.creation_date > last_balance.creation_date) \
                              .filter(Charges.transaction_type_id == 1).one()
        last_in = session.query(sqlfunc.sum(Charges.amount).label("amount")) \
                         .filter(Charges.client_id == client.id) \
                         .filter(Charges.creation_date > last_balance.creation_date) \
                         .filter(Charges.transaction_type_id != 1).one()

        new_balance = Balances()
        new_balance.creation_date = datetime.now()
        new_balance.client_id = client.id
        new_balance.amount = last_balance.amount - last_charges.amount + last_in.amount
        session.add(new_balance)

        try:
            session.commit()
        except Exception:
            logging.warning("Не удалось записать текущее значение баланса для организации {}".format(client.id))
            session.rollback()
        finally:
            session.close()

def main(bookerTimer: func.TimerRequest) -> None:
    utc_timestamp = datetime.utcnow().replace(tzinfo=timezone.utc).isoformat()

    if bookerTimer.past_due:
        logging.info('The timer is past due!')

    #process()
    process_balance_changing()
    logging.info('Python timer trigger function ran at %s', utc_timestamp)
