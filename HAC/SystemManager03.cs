﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeMatching;
using System.Diagnostics;

namespace HAC
{
    // Relative Strength Index Strategy
    class SystemManager03
    {
        private Instrument m_Instrument;
        private List<Tick> m_TickList;
        
        private bool m_Go;
        private bool m_Start;

        private int m_Ticks;

        private double m_Diff;
        private double m_UpTotal;
        private double m_DownTotal;
        private double m_RS;
        private double m_RSI;

        private double m_Position;
        private double m_NetPos;

        private bool m_Bool;
        private Value_State m_State;

        private double m_Qty;

        private double m_Target;
        private double m_Stop;
        private int m_TargetTicks;
        private int m_StopTicks;

        private TradeMatcher m_Matcher;

        public event OnSystemUpdateEventHandler OnSystemUpdate;
        // public event FillEventHandler OnFill;

        public SystemManager03()
        {
            m_Matcher = new TradeMatcher(RoundTurnMethod.FIFO);

            // Create a new Instrument object.
            m_Instrument = new Instrument("ES", "201406", Instrument.InstrumentType.FUTURE);
            m_Instrument.BidAskUpdate += new DataUpdateEventHandler(OnInstrumentUpdate);
            m_Instrument.FillUpdate += new FillEventHandler(OnInstrumentFill);

            // Create a new SortedList to hold the Tick objects.
            m_TickList = new List<Tick>();

            m_Position = 0;
            m_Go = false;
            m_Qty = 0;
        }

        ~SystemManager03()
        {
            //Debug::WriteLine( "SystemManager dying." );
        }

        private void OnInstrumentUpdate(Tick m_Tick)
        {
            m_TickList.Add(m_Tick);

            m_RSI = 50;
            m_RS = 0;
            
            // Begin calculation
            if (m_Ticks > 0 && m_TickList.Count > m_Ticks)
            {
                // Calculate the RS and RSI.
                m_UpTotal=0;
                m_DownTotal=0;
                for (int i = m_TickList.Count - m_Ticks; i < m_TickList.Count - 1; i++)
                {
                    m_Diff = m_TickList[i].Price - m_TickList[i-1].Price;
                    if (m_Diff > 0)
                        m_UpTotal += m_Diff;
                    else if (m_Diff < 0)
                        m_DownTotal -= m_Diff;
                }
                m_RS = m_UpTotal / m_DownTotal;
                m_RSI = 100 - 100 / (1 + m_RS);
                Debug.WriteLine(m_RSI);
                
                //// Set the Value State.
                //if (m_RSI < 40)
                //    m_State = Value_State.LOW;
                //else if (m_RSI < 60)
                //    m_State = Value_State.MID;
                //else
                //    m_State = Value_State.HIGH;
            }

            // START/STOP Switch
            if (m_Go)
            {
                // If we already have a position on, and have either met out target or stop price, get out.
                if (m_Position > 0 && (m_Tick.Price > m_Target || m_Tick.Price < m_Stop))
                {
                    m_Instrument.EnterMarketOrder("S", m_Qty.ToString());
                }
                if (m_Position < 0 && (m_Tick.Price < m_Target || m_Tick.Price > m_Stop))
                {
                    m_Instrument.EnterMarketOrder("B", m_Qty.ToString());
                }

                // First time only and on reset, set initial state.
                if (m_Start)
                {
                    if (m_RSI >= 60)
                        m_State = Value_State.HIGH;
                    else if (m_RSI >= 40)
                        m_State = Value_State.MID;
                    else
                        m_State = Value_State.LOW;
                    m_Start = false;
                }

                // Has there been oversold?
                if (m_RSI < 40 && m_State != Value_State.LOW)
                {
                    // Change state.
                    m_State = Value_State.LOW;

                    // If we are already short, first get flat.
                    if (m_Position < 0)
                    {
                        m_Instrument.EnterMarketOrder("B", m_Qty.ToString());
                    }
                    // Go long.
                    m_Instrument.EnterMarketOrder("B", m_Qty.ToString());

                    // Set target price and stop loss price.
                    m_Target = m_Tick.Price + m_TargetTicks * m_Instrument.get_TickSize();
                    m_Stop = m_Tick.Price - m_StopTicks * m_Instrument.get_TickSize();
                }

                // Has there been overbought?
                if (m_RSI >= 60 && m_State != Value_State.HIGH)
                {
                    // Change state.
                    m_State = Value_State.HIGH;

                    // If we are already long, first get flat.
                    if (m_Position > 0)
                    {
                        m_Instrument.EnterMarketOrder("S", m_Qty.ToString());
                    }
                    // Go short.
                    m_Instrument.EnterMarketOrder("S", m_Qty.ToString());

                    // Set target price and stop loss price.
                    m_Target = m_Tick.Price - m_TargetTicks * m_Instrument.get_TickSize();
                    m_Stop = m_Tick.Price + m_StopTicks * m_Instrument.get_TickSize();
                }
            }
            // Send the data to the GUI.
            OnSystemUpdate(m_Tick.Price, m_Tick.Qty, m_RSI, m_RS, m_Target, m_Stop);
        }

        private void OnInstrumentFill(Fill x)
        {
            // Update position.
            if (x.BuySell == "B")
            {
                m_Position += x.Qty;
            }
            else
            {
                m_Position -= x.Qty;
            }

            //// Send the data to the TradeMacher.
            //Fill m_Fill = new Fill();
            //if (BS == "B")
            //    m_Fill.BS = TradeType.BUY;
            //else
            //    m_Fill.BS = TradeType.SELL;

            //m_Fill.Price = Convert.ToDouble(price);
            //m_Fill.TradeID = key;
            //m_Fill.Qty = qty;
            //m_Matcher.Fill_Received(m_Fill);

            m_NetPos = m_Position;
        }

        public void StartStop()
        {
            if (m_Go == false)
            {
                m_Go = true;
                m_Start = true;
            }
            else
            {
                m_Go = false;
            }
        }

        public void ShutDown()
        {
            m_Go = false;
            //m_Instrument.ShutDown();
            //m_Instrument.OnInstrumentUpdate -= new InstrumentUpdateEventHandler(OnInstrumentUpdate);
            //m_Instrument.OnFill -= new FillEventHandler(OnInstrumentFill);
            m_Instrument = null;
        }

        public double Qty
        {
            get { return m_Qty; }
            set { m_Qty = value; }
        }

        public double Bid
        {
            get { return m_Instrument.get_Bid(); }
        }

        public double Ask
        {
            get { return m_Instrument.get_Ask(); }
        }

        public double Position
        {
            get { return m_Position; }
        }

        public double NetPos
        {
            get { return m_NetPos; }
        }

        public int StopTicks
        {
            get { return m_StopTicks; }
            set { m_StopTicks = value; }
        }

        public int TargetTicks
        {
            get { return m_TargetTicks; }
            set { m_TargetTicks = value; }
        }

        public int Ticks
        {
            get { return m_Ticks; }
            set { m_Ticks = value; }
        }

        public TradeMatcher Matcher
        {
            get { return m_Matcher; }
        }
    }
}
